using System.Xml.Linq;
using Dental.Application.Abstractions;
using Dental.Integrations.Common;
using Dental.Integrations.Enabiz;
using Dental.Integrations.Enabiz.Fake;
using Microsoft.Extensions.Logging.Abstractions;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Dental.UnitTests.Integrations;

/// <summary>
/// SYS sürücüsünün sözleşme uyumu.
///
/// Beklentiler systest WSDL'inden doğrulanmıştır: operasyon <c>SYSSendMessage</c>, ad alanı
/// <c>https://sys.sagliknet.saglik.gov.tr/SYS/</c>, SOAPAction
/// <c>.../ISYSWS/SYSSendMessage</c>; yanıt <c>sonucKodu</c> = <c>S0000</c> ile başarıdır.
/// </summary>
public sealed class SysSoapClientTests : IDisposable
{
    private static readonly XNamespace Soap = "http://schemas.xmlsoap.org/soap/envelope/";
    private static readonly XNamespace Sys = "https://sys.sagliknet.saglik.gov.tr/SYS/";

    private readonly WireMockServer _server = WireMockServer.Start();
    private readonly HttpClient _http = new();

    public void Dispose()
    {
        _server.Stop();
        _server.Dispose();
        _http.Dispose();
    }

    private SysSoapClient CreateClient(bool rawXml = false) => new(
        _http,
        new EnabizSettings
        {
            SysTestUrl = _server.Url!,
            Environment = "Test",
            UssUsername = "kullanici",
            UssPassword = "sifre",
            CkysCode = "123456",
            EmbedPayloadAsRawXml = rawXml,
        },
        NullLogger<SysSoapClient>.Instance);

    private void StubResponse(string resultXml) =>
        _server.Given(Request.Create().WithPath("/").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200)
                .WithHeader("Content-Type", "text/xml; charset=utf-8")
                .WithBody(
                    $"""
                     <s:Envelope xmlns:s="http://schemas.xmlsoap.org/soap/envelope/"><s:Body>
                     <SYSSendMessageResponse xmlns="https://sys.sagliknet.saglik.gov.tr/SYS/">
                     <SYSSendMessageResult>{System.Security.SecurityElement.Escape(resultXml)}</SYSSendMessageResult>
                     </SYSSendMessageResponse></s:Body></s:Envelope>
                     """));

    /// <summary>Sunucuya ulaşan tek isteğin mesajı.</summary>
    private WireMock.IRequestMessage SingleRequest() =>
        Assert.Single(_server.LogEntries).RequestMessage;

    private XDocument SingleRequestEnvelope() => XDocument.Parse(SingleRequest().Body!);

    private static string SuccessBody(string takipNo) =>
        $"""
         <SYSMessage><recordData><KayitCevabi>
         <sonucKodu value="S0000"/>
         <sonucMesaji value="İşlem Başarı ile Sonuçlandı."/>
         <SYSTakipNo value="{takipNo}"/>
         </KayitCevabi></recordData></SYSMessage>
         """;

    // ---- Taşıma sözleşmesi ----

    [Fact]
    public async Task SendPacket_UsesWsdlOperationNamespaceAndSoapAction()
    {
        StubResponse(SuccessBody("T1"));

        await CreateClient().SendPacketAsync(new EnabizPacket(101, "<SYSMessage />", "123456"));

        var request = SingleRequest();
        var action = request.Headers!["SOAPAction"].Single();
        Assert.Equal("\"https://sys.sagliknet.saglik.gov.tr/SYS/ISYSWS/SYSSendMessage\"", action);

        var envelope = XDocument.Parse(request.Body!);
        var call = envelope.Root!.Element(Soap + "Body")!.Element(Sys + "SYSSendMessage");
        Assert.NotNull(call);
        Assert.NotNull(call!.Element(Sys + "input"));
    }

    [Fact]
    public async Task SendPacket_SendsWsSecurityUsernameTokenHeader()
    {
        // systest ölçümü: başlıksız istek "şifre tanimli değil", başlıklı istek "şifre yanlış"
        // döndürüyor — yani kimlik gövdede değil, WS-Security başlığındadır.
        StubResponse(SuccessBody("T1"));

        await CreateClient().SendPacketAsync(new EnabizPacket(101, "<SYSMessage />"));

        var envelope = SingleRequestEnvelope();
        var token = envelope.Root!.Element(Soap + "Header")!
            .Element(WsSecurity.Wsse + "Security")!
            .Element(WsSecurity.Wsse + "UsernameToken")!;

        Assert.Equal("kullanici", token.Element(WsSecurity.Wsse + "Username")!.Value);
        var password = token.Element(WsSecurity.Wsse + "Password")!;
        Assert.Equal("sifre", password.Value);
        Assert.Equal(WsSecurity.PasswordTextType, password.Attribute("Type")!.Value);
    }

    [Fact]
    public async Task SendPacket_ByDefault_EscapesPayloadAsString()
    {
        // WSDL input'u xs:string tanımlar; varsayılan davranış kaçışlanmış metindir.
        StubResponse(SuccessBody("T1"));

        await CreateClient().SendPacketAsync(new EnabizPacket(101, "<SYSMessage><a value=\"1\" /></SYSMessage>"));

        var body = SingleRequest().Body!;
        Assert.Contains("&lt;SYSMessage&gt;", body, StringComparison.Ordinal);

        var input = XDocument.Parse(body).Descendants(Sys + "input").Single();
        Assert.False(input.HasElements);
        Assert.Equal("<SYSMessage><a value=\"1\" /></SYSMessage>", input.Value);
    }

    [Fact]
    public async Task SendPacket_WithRawXmlFlag_NestsPayloadAsElement()
    {
        // Resmi örnek XML biçimi; bayrakla açılabilir.
        StubResponse(SuccessBody("T1"));

        await CreateClient(rawXml: true)
            .SendPacketAsync(new EnabizPacket(101, "<SYSMessage><a value=\"1\" /></SYSMessage>"));

        var input = SingleRequestEnvelope().Descendants(Sys + "input").Single();
        Assert.True(input.HasElements);
        Assert.Equal("SYSMessage", input.Elements().Single().Name.LocalName);
    }

    [Fact]
    public async Task SendPacket_WithoutCredentials_FailsFastWithoutCallingService()
    {
        var client = new SysSoapClient(
            _http,
            new EnabizSettings { SysTestUrl = _server.Url!, Environment = "Test" },
            NullLogger<SysSoapClient>.Instance);

        await Assert.ThrowsAsync<EnabizClientException>(
            () => client.SendPacketAsync(new EnabizPacket(101, "<SYSMessage />")));
        Assert.Empty(_server.LogEntries);
    }

    // ---- Yanıt çözümleme ----

    [Fact]
    public async Task SendPacket_SuccessCode_ReturnsAcceptedWithTakipNo()
    {
        StubResponse(SuccessBody("SYS987654321"));

        var result = await CreateClient().SendPacketAsync(new EnabizPacket(101, "<SYSMessage />"));

        Assert.True(result.Accepted);
        Assert.Equal("SYS987654321", result.SysTakipNo);
    }

    [Fact]
    public async Task SendPacket_ErrorCode_ReturnsRejectedWithMessage()
    {
        StubResponse(
            """
            <SYSMessage><recordData><KayitCevabi>
            <sonucKodu value="H1234"/>
            <sonucMesaji value="Hasta kimlik numarası bulunamadı."/>
            </KayitCevabi></recordData></SYSMessage>
            """);

        var result = await CreateClient().SendPacketAsync(new EnabizPacket(101, "<SYSMessage />"));

        Assert.False(result.Accepted);
        Assert.Equal("H1234", result.ErrorCode);
        Assert.Equal("Hasta kimlik numarası bulunamadı.", result.ErrorMessage);
    }

    [Fact]
    public async Task SendPacket_SoapFault_IsBusinessRejectionNotTransportError()
    {
        // systest'in kimlik reddi tam olarak bu biçimde gelir.
        _server.Given(Request.Create().WithPath("/").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(500)
                .WithHeader("Content-Type", "text/xml; charset=utf-8")
                .WithBody(
                    """
                    <s:Envelope xmlns:s="http://schemas.xmlsoap.org/soap/envelope/"><s:Body><s:Fault>
                    <faultcode>s:Client</faultcode>
                    <faultstring xml:lang="tr-TR">Kullanıcı adı veya şifre yanlış!</faultstring>
                    </s:Fault></s:Body></s:Envelope>
                    """));

        var result = await CreateClient().SendPacketAsync(new EnabizPacket(101, "<SYSMessage />"));

        Assert.False(result.Accepted);
        Assert.Equal("s:Client", result.ErrorCode);
        Assert.Equal("Kullanıcı adı veya şifre yanlış!", result.ErrorMessage);
    }

    [Fact]
    public async Task SendPacket_UnparseableResponse_IsNotTreatedAsSuccess()
    {
        StubResponse("bu bir xml degil");

        var result = await CreateClient().SendPacketAsync(new EnabizPacket(101, "<SYSMessage />"));

        Assert.False(result.Accepted);
        Assert.Null(result.SysTakipNo);
    }

    // ---- Sahte sürücü ----

    [Fact]
    public async Task FakeClient_IsDeterministic()
    {
        var fake = new FakeEnabizClient(NullLogger<FakeEnabizClient>.Instance);
        var packet = new EnabizPacket(203, "<SYSMessage><a value=\"1\" /></SYSMessage>");

        var first = await fake.SendPacketAsync(packet);
        var second = await fake.SendPacketAsync(packet);

        Assert.True(first.Accepted);
        Assert.Equal(first.SysTakipNo, second.SysTakipNo);
    }

    [Fact]
    public async Task FakeClient_RejectScenarioTckn_IsBusinessRejection()
    {
        var fake = new FakeEnabizClient(NullLogger<FakeEnabizClient>.Instance);
        var packet = new EnabizPacket(101,
            $"<SYSMessage><HASTA_KIMLIK_NUMARASI value=\"{FakeEnabizClient.RejectTckn}\" /></SYSMessage>");

        var result = await fake.SendPacketAsync(packet);

        Assert.False(result.Accepted);
        Assert.Equal("1001", result.ErrorCode);
    }

    [Fact]
    public async Task FakeClient_TransientScenarioTckn_ThrowsForRetryPath()
    {
        var fake = new FakeEnabizClient(NullLogger<FakeEnabizClient>.Instance);
        var packet = new EnabizPacket(101,
            $"<SYSMessage><HASTA_KIMLIK_NUMARASI value=\"{FakeEnabizClient.TransientFailureTckn}\" /></SYSMessage>");

        await Assert.ThrowsAsync<EnabizClientException>(() => fake.SendPacketAsync(packet));
    }
}
