using System.Net;
using System.Text;
using System.Xml.Linq;
using Dental.Application.Abstractions;
using Dental.EDocument.Ubl;
using Dental.EDocument.Ubl.Builders;
using Dental.EDocument.Ubl.Models;
using Dental.Integrations.EInvoice.Uyumsoft;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Dental.IntegrationTests;

/// <summary>
/// Uyumsoft SOAP sözleşmesinin doğrulanması.
///
/// Uyumsoft TEST ucu (efatura-test.uyumsoft.com.tr, 176.235.113.54) bu ağdan yönlendirilemiyor
/// ("Network is unreachable"), bu yüzden gerçek gönderim yapılamıyor. Onun yerine sürücü, WSDL'den
/// (efatura.uyumsoft.com.tr/Services/Integration?wsdl) çıkarılmış sözleşmeyi taklit eden YEREL bir
/// SOAP sunucusuna karşı koşturulur: giden zarfın (SOAPAction, WS-Security UsernameToken, tempuri
/// öğe sırası, TİPLİ gömülü UBL) ve gelen yanıtın çözümlenmesi gerçek kod yoluyla doğrulanır.
/// </summary>
public sealed class UyumsoftProviderTests
{
    private static readonly XNamespace Soap = "http://schemas.xmlsoap.org/soap/envelope/";
    private static readonly XNamespace Tempuri = "http://tempuri.org/";
    private static readonly XNamespace Wsse =
        "http://docs.oasis-open.org/wss/2004/01/oasis-200401-wss-wssecurity-secext-1.0.xsd";

    [Fact]
    public async Task SendDocument_BuildsContractShapedEnvelope_AndParsesIdentity()
    {
        using var server = new StubSoapServer(_ => Ok(
            "SendInvoiceResponse", "SendInvoiceResult",
            new XAttribute("IsSucceded", "true"),
            new XElement(Tempuri + "Value",
                new XAttribute("Id", "3fa85f64-5717-4562-b3fc-2c963f66afa6"),
                new XAttribute("Number", "DIS2026000000042"),
                new XAttribute("InvoiceScenario", "eArchive"))));

        var provider = server.CreateProvider();
        var ubl = BuildArchiveInvoiceXml();
        var result = await provider.SendDocumentAsync(new EDocumentEnvelope(
            EDocType.EArchive, ubl, "11111111-2222-3333-4444-555555555555"));

        Assert.True(result.Success, result.Error);
        Assert.Equal("3fa85f64-5717-4562-b3fc-2c963f66afa6", result.ProviderRef);

        // ---- Giden zarfın sözleşmeye uygunluğu ----
        Assert.Equal("\"http://tempuri.org/IIntegration/SendInvoice\"", server.LastSoapAction);
        var envelope = XDocument.Parse(server.LastRequestBody!);

        // WS-Security UsernameToken (PasswordText) başlığı.
        var token = envelope.Descendants(Wsse + "UsernameToken").Single();
        Assert.Equal("Uyumsoft", token.Element(Wsse + "Username")!.Value);
        Assert.Equal("Uyumsoft", token.Element(Wsse + "Password")!.Value);
        Assert.EndsWith("#PasswordText", token.Element(Wsse + "Password")!.Attribute("Type")!.Value);
        Assert.Equal("1", envelope.Descendants(Wsse + "Security").Single()
            .Attribute(Soap + "mustUnderstand")!.Value);

        var info = envelope.Descendants(Tempuri + "InvoiceInfo").Single();
        Assert.Equal("11111111-2222-3333-4444-555555555555", info.Attribute("LocalDocumentId")!.Value);

        // Şema sırası: Invoice → (TargetCustomer | EArchiveInvoiceInfo) → Scenario → CreateDateUtc.
        Assert.Equal(
            new[] { "Invoice", "EArchiveInvoiceInfo", "Scenario", "CreateDateUtc" },
            info.Elements().Select(e => e.Name.LocalName).ToArray());
        Assert.Equal("eArchive", info.Element(Tempuri + "Scenario")!.Value);
        Assert.Equal("Electronic", info.Element(Tempuri + "EArchiveInvoiceInfo")!.Attribute("DeliveryType")!.Value);

        // KRİTİK: belge base64/CDATA DEĞİL, tipli UBL olarak gömülür (InvoiceType).
        var invoice = info.Element(Tempuri + "Invoice")!;
        XNamespace cbc = UblNamespaces.Cbc;
        Assert.Equal("TR1.2", invoice.Element(cbc + "CustomizationID")!.Value);
        Assert.Equal("EARSIVFATURA", invoice.Element(cbc + "ProfileID")!.Value);
        Assert.Equal("DIS2026000000042", invoice.Element(cbc + "ID")!.Value);
        Assert.DoesNotContain("base64", invoice.Name.NamespaceName, StringComparison.OrdinalIgnoreCase);
        // Kök öğe taşınmaz, yalnız çocukları — iç içe <Invoice><Invoice> olmamalı.
        Assert.Empty(invoice.Elements().Where(e => e.Name.LocalName == "Invoice"));
    }

    [Fact]
    public async Task SendDocument_ForEInvoice_CarriesTargetCustomerAndAlias()
    {
        using var server = new StubSoapServer(_ => Ok(
            "SendInvoiceResponse", "SendInvoiceResult",
            new XAttribute("IsSucceded", "true"),
            new XElement(Tempuri + "Value", new XAttribute("Id", "abc"))));

        var provider = server.CreateProvider();
        await provider.SendDocumentAsync(new EDocumentEnvelope(
            EDocType.EInvoice,
            BuildArchiveInvoiceXml(profileId: UblProfileIds.TicariFatura),
            "11111111-2222-3333-4444-555555555555",
            TargetAlias: "urn:mail:defaultpk@alici.com.tr"));

        var info = XDocument.Parse(server.LastRequestBody!).Descendants(Tempuri + "InvoiceInfo").Single();
        Assert.Equal(
            new[] { "Invoice", "TargetCustomer", "Scenario", "CreateDateUtc" },
            info.Elements().Select(e => e.Name.LocalName).ToArray());
        var customer = info.Element(Tempuri + "TargetCustomer")!;
        // Alıcı kimliği UBL'den okunur (AccountingCustomerParty/PartyIdentification).
        Assert.Equal("11111111111", customer.Attribute("VknTckn")!.Value);
        Assert.Equal("urn:mail:defaultpk@alici.com.tr", customer.Attribute("Alias")!.Value);
        Assert.Equal("eInvoice", info.Element(Tempuri + "Scenario")!.Value);
    }

    [Fact]
    public async Task SendDocument_WhenIntegratorRejects_ReturnsFailureWithoutThrowing()
    {
        using var server = new StubSoapServer(_ => Ok(
            "SendInvoiceResponse", "SendInvoiceResult",
            new XAttribute("IsSucceded", "false"),
            new XAttribute("Message", "Fatura numarası daha önce kullanılmış.")));

        var result = await server.CreateProvider().SendDocumentAsync(new EDocumentEnvelope(
            EDocType.EArchive, BuildArchiveInvoiceXml(), Guid.NewGuid().ToString()));

        Assert.False(result.Success);
        Assert.Equal("Fatura numarası daha önce kullanılmış.", result.Error);
    }

    [Fact]
    public async Task SendDocument_OnSoapFault_ReturnsFailureWithFaultText()
    {
        using var server = new StubSoapServer(_ => Fault("s:Client", "Kimlik doğrulama başarısız."), status: 500);

        var result = await server.CreateProvider().SendDocumentAsync(new EDocumentEnvelope(
            EDocType.EArchive, BuildArchiveInvoiceXml(), Guid.NewGuid().ToString()));

        Assert.False(result.Success);
        Assert.Contains("Kimlik doğrulama başarısız.", result.Error);
    }

    [Fact]
    public async Task SendDocument_ForEsmm_IsRefusedByThisEndpoint()
    {
        using var server = new StubSoapServer(_ => Ok("SendInvoiceResponse", "SendInvoiceResult",
            new XAttribute("IsSucceded", "true")));

        // Integration servisi yalnız UBL Invoice taşır; e-SMM (CreditNote) sessizce yanlış
        // belge göndermek yerine açıkça reddedilir (AÇIK MADDE: ayrı e-SMM ucu gerekli).
        var error = await Assert.ThrowsAsync<EInvoiceProviderException>(() =>
            server.CreateProvider().SendDocumentAsync(new EDocumentEnvelope(
                EDocType.ESmm, BuildArchiveInvoiceXml(), Guid.NewGuid().ToString())));
        Assert.Contains("e-SMM", error.Message);
    }

    [Fact]
    public async Task GetStatus_MapsUyumsoftStatusesToPortStatuses()
    {
        var statuses = new Dictionary<string, EDocProviderStatus>
        {
            ["Queued"] = EDocProviderStatus.Queued,
            ["Processing"] = EDocProviderStatus.Processing,
            ["SentToGib"] = EDocProviderStatus.Processing,
            ["Approved"] = EDocProviderStatus.Succeeded,
            ["Declined"] = EDocProviderStatus.BuyerRejected,
            ["Error"] = EDocProviderStatus.GibRejected,
            ["EArchivedCanceled"] = EDocProviderStatus.Cancelled,
        };

        foreach (var (raw, expected) in statuses)
        {
            using var server = new StubSoapServer(_ => Ok(
                "QueryOutboxInvoiceStatusResponse", "QueryOutboxInvoiceStatusResult",
                new XAttribute("IsSucceded", "true"),
                new XElement(Tempuri + "Value",
                    new XAttribute("Status", raw),
                    new XAttribute("StatusCode", "0"),
                    new XAttribute("InvoiceId", "abc"),
                    new XAttribute("Message", raw))));

            var result = await server.CreateProvider().GetStatusAsync("abc", EDocType.EArchive);
            Assert.Equal(expected, result.Status);
            Assert.Equal("\"http://tempuri.org/IIntegration/QueryOutboxInvoiceStatus\"", server.LastSoapAction);
        }
    }

    [Fact]
    public async Task GetPdf_DecodesBase64Payload()
    {
        var pdf = Encoding.UTF8.GetBytes("%PDF-1.7 test");
        using var server = new StubSoapServer(_ => Ok(
            "GetOutboxInvoicePdfResponse", "GetOutboxInvoicePdfResult",
            new XAttribute("IsSucceded", "true"),
            new XElement(Tempuri + "Value",
                new XAttribute("InvoiceId", "abc"),
                new XElement(Tempuri + "Data", Convert.ToBase64String(pdf)))));

        var bytes = await server.CreateProvider().GetPdfAsync("abc", EDocType.EArchive);
        Assert.Equal(pdf, bytes);
    }

    [Fact]
    public async Task CancelEArchive_SendsInvoiceIdAndCancelDate()
    {
        using var server = new StubSoapServer(_ => Ok(
            "CancelEArchiveInvoiceResponse", "CancelEArchiveInvoiceResult",
            new XAttribute("IsSucceded", "true"), new XAttribute("Value", "true")));

        await server.CreateProvider().CancelEArchiveAsync("abc", "Hatalı düzenlendi.");

        var request = XDocument.Parse(server.LastRequestBody!).Descendants(Tempuri + "request").Single();
        Assert.Equal("abc", request.Attribute("InvoiceId")!.Value);
        Assert.True(DateTime.TryParse(request.Attribute("CancelDate")!.Value, out _));
    }

    [Fact]
    public async Task WhenEndpointUnreachable_ThrowsPortException_NotDriverSpecificOne()
    {
        // Kapalı bir porta bakan sürücü: taşıma hatası porta ait istisnaya çevrilmeli ki
        // üst katman (EDocumentDispatcher) geçici hata olarak yeniden deneyebilsin.
        var settings = new UyumsoftSettings { TestUrl = "http://127.0.0.1:1/services/Integration" };
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var provider = new UyumsoftEInvoiceProvider(http, settings, NullLogger<UyumsoftEInvoiceProvider>.Instance);

        var error = await Assert.ThrowsAsync<EInvoiceProviderException>(() =>
            provider.SendDocumentAsync(new EDocumentEnvelope(
                EDocType.EArchive, BuildArchiveInvoiceXml(), Guid.NewGuid().ToString())));
        Assert.Contains("SendInvoice", error.Message);
    }

    // ---- Yardımcılar ----

    private static XElement Ok(string responseName, string resultName, params object[] content) =>
        new(Tempuri + responseName, new XElement(Tempuri + resultName, content));

    private static XElement Fault(string code, string message) =>
        new(Soap + "Fault",
            new XElement("faultcode", code),
            new XElement("faultstring", message));

    /// <summary>Gerçek builder'la üretilmiş, gönderime hazır bir e-Arşiv UBL'i.</summary>
    private static string BuildArchiveInvoiceXml(string profileId = UblProfileIds.EArsivFatura)
    {
        var model = new EDocumentModel
        {
            Kind = profileId == UblProfileIds.EArsivFatura ? DocumentKind.EArsiv : DocumentKind.EFatura,
            ProfileId = profileId,
            TypeCode = UblTypeCodes.Satis,
            InvoiceNumber = "DIS2026000000042",
            Ettn = Guid.Parse("11111111-2222-3333-4444-555555555555"),
            IssueDate = new DateOnly(2026, 8, 20),
            IssueTime = new TimeOnly(10, 30),
            Seller = new SellerInfo
            {
                Name = "Demo Diş Kliniği A.Ş.",
                TaxId = "1234567801",
                TaxOffice = "Kadıköy",
                Address = new AddressInfo { CitySubdivisionName = "Kadıköy", CityName = "İstanbul", CountryCode = "TR" },
            },
            Buyer = new BuyerInfo
            {
                Kind = BuyerKind.IndividualPatient,
                FirstName = "Ayşe",
                LastName = "Yerli",
                TaxId = "11111111111",
                Address = new AddressInfo { CitySubdivisionName = "Üsküdar", CityName = "İstanbul", CountryCode = "TR" },
            },
            Lines =
            [
                new DocumentLine
                {
                    Name = "Diş dolgusu",
                    Quantity = 1m,
                    UnitPrice = 1000m,
                    VatRate = 10m,
                    VatAmount = 100m,
                    LineTotal = 1000m,
                },
            ],
            Totals = new DocumentTotals
            {
                LineExtensionTotal = 1000m,
                VatTotal = 100m,
                TaxExclusiveAmount = 1000m,
                TaxInclusiveAmount = 1100m,
                PayableAmount = 1100m,
            },
        };

        return new InvoiceUblBuilder().BuildXmlString(model);
    }

    /// <summary>WSDL sözleşmesini taklit eden yerel SOAP sunucusu; isteği yakalar, sabit yanıt döner.</summary>
    private sealed class StubSoapServer : IDisposable
    {
        private readonly HttpListener _listener = new();
        private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };
        private readonly CancellationTokenSource _cts = new();

        public StubSoapServer(Func<string, XElement> responder, int status = 200)
        {
            Port = GetFreePort();
            _listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
            _listener.Start();
            _ = Task.Run(async () =>
            {
                while (!_cts.IsCancellationRequested)
                {
                    HttpListenerContext context;
                    try { context = await _listener.GetContextAsync(); }
                    catch (Exception) { return; }

                    using var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8);
                    LastRequestBody = await reader.ReadToEndAsync();
                    LastSoapAction = context.Request.Headers["SOAPAction"];

                    var body = responder(LastRequestBody);
                    var envelope = new XElement(Soap + "Envelope",
                        new XAttribute(XNamespace.Xmlns + "s", Soap),
                        new XElement(Soap + "Body", body));
                    var payload = Encoding.UTF8.GetBytes(envelope.ToString(SaveOptions.DisableFormatting));

                    context.Response.StatusCode = status;
                    context.Response.ContentType = "text/xml; charset=utf-8";
                    await context.Response.OutputStream.WriteAsync(payload);
                    context.Response.Close();
                }
            });
        }

        public int Port { get; }
        public string? LastRequestBody { get; private set; }
        public string? LastSoapAction { get; private set; }

        public UyumsoftEInvoiceProvider CreateProvider() => new(
            _http,
            new UyumsoftSettings { TestUrl = $"http://127.0.0.1:{Port}/services/Integration" },
            NullLogger<UyumsoftEInvoiceProvider>.Instance);

        private static int GetFreePort()
        {
            using var socket = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            socket.Start();
            var port = ((IPEndPoint)socket.LocalEndpoint).Port;
            socket.Stop();
            return port;
        }

        public void Dispose()
        {
            _cts.Cancel();
            _listener.Close();
            _http.Dispose();
            _cts.Dispose();
        }
    }
}
