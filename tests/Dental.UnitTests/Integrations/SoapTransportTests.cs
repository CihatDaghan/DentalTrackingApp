using System.Xml.Linq;
using Dental.Integrations.Common;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Dental.UnitTests.Integrations;

public sealed class SoapTransportTests : IDisposable
{
    private static readonly XNamespace Soap = "http://schemas.xmlsoap.org/soap/envelope/";
    private static readonly XNamespace Svc = "http://tempuri.org/einvoice";

    private readonly WireMockServer _server = WireMockServer.Start();
    private readonly HttpClient _http = new();

    public void Dispose()
    {
        _server.Stop();
        _server.Dispose();
        _http.Dispose();
    }

    [Fact]
    public void BuildEnvelope_WithoutHeader_ProducesSoap11EnvelopeWithBodyOnly()
    {
        var body = new XElement(Svc + "SendDocument", new XElement(Svc + "Ettn", "abc-123"));

        var envelope = SoapTransport.BuildEnvelope(body);

        Assert.Equal("1.0", envelope.Declaration?.Version);
        Assert.Equal("utf-8", envelope.Declaration?.Encoding);
        Assert.Equal(Soap + "Envelope", envelope.Root!.Name);
        Assert.Null(envelope.Root.Element(Soap + "Header"));
        var sent = envelope.Root.Element(Soap + "Body")!.Element(Svc + "SendDocument");
        Assert.NotNull(sent);
        Assert.Equal("abc-123", sent!.Element(Svc + "Ettn")?.Value);
    }

    [Fact]
    public void BuildEnvelope_WithHeader_IncludesHeaderElement()
    {
        var header = new XElement(Svc + "Auth", new XElement(Svc + "User", "Uyumsoft"));
        var body = new XElement(Svc + "GetStatus");

        var envelope = SoapTransport.BuildEnvelope(body, [header]);

        var headerEl = envelope.Root!.Element(Soap + "Header");
        Assert.NotNull(headerEl);
        Assert.Equal("Uyumsoft", headerEl!.Element(Svc + "Auth")?.Element(Svc + "User")?.Value);
    }

    [Fact]
    public void UnwrapBody_ValidResponse_ReturnsFirstBodyElement()
    {
        const string responseXml = """
            <?xml version="1.0" encoding="utf-8"?>
            <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
              <soap:Body>
                <SendDocumentResponse xmlns="http://tempuri.org/einvoice">
                  <Result>Queued</Result>
                </SendDocumentResponse>
              </soap:Body>
            </soap:Envelope>
            """;

        var element = SoapTransport.UnwrapBody(responseXml);

        Assert.Equal("SendDocumentResponse", element.Name.LocalName);
        Assert.Equal(Svc + "SendDocumentResponse", element.Name);
        Assert.Equal("Queued", element.Element(Svc + "Result")?.Value);
    }

    [Fact]
    public void UnwrapBody_FaultResponse_ThrowsSoapFaultException()
    {
        const string faultXml = """
            <?xml version="1.0" encoding="utf-8"?>
            <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
              <soap:Body>
                <soap:Fault>
                  <faultcode>soap:Server</faultcode>
                  <faultstring>Kimlik doğrulama başarısız</faultstring>
                  <detail><ErrorCode>AUTH-01</ErrorCode></detail>
                </soap:Fault>
              </soap:Body>
            </soap:Envelope>
            """;

        var ex = Assert.Throws<SoapFaultException>(() => SoapTransport.UnwrapBody(faultXml));

        Assert.Equal("soap:Server", ex.FaultCode);
        Assert.Equal("Kimlik doğrulama başarısız", ex.FaultString);
        Assert.Contains("AUTH-01", ex.Detail);
        Assert.Contains("Kimlik doğrulama başarısız", ex.Message);
    }

    [Fact]
    public void UnwrapBody_NonXml_ThrowsSoapTransportException()
    {
        Assert.Throws<SoapTransportException>(() => SoapTransport.UnwrapBody("<html>gateway error</html"));
    }

    [Fact]
    public async Task SendAsync_PostsEnvelopeWithSoapActionAndUnwrapsResponse()
    {
        _server.Given(Request.Create().WithPath("/services/Integration").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(200)
                .WithHeader("Content-Type", "text/xml; charset=utf-8")
                .WithBody("""
                    <?xml version="1.0" encoding="utf-8"?>
                    <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
                      <soap:Body><Pong xmlns="http://tempuri.org/einvoice">ok</Pong></soap:Body>
                    </soap:Envelope>
                    """));

        var transport = new SoapTransport(_http);
        var result = await transport.SendAsync(
            new Uri(_server.Url! + "/services/Integration"),
            "http://tempuri.org/einvoice/Ping",
            new XElement(Svc + "Ping"));

        Assert.Equal("Pong", result.Name.LocalName);
        Assert.Equal("ok", result.Value);

        var entry = Assert.Single(_server.LogEntries);
        Assert.Equal("\"http://tempuri.org/einvoice/Ping\"", entry.RequestMessage!.Headers!["SOAPAction"].Single());
        Assert.StartsWith("text/xml", entry.RequestMessage!.Headers["Content-Type"].Single());
        Assert.Contains("<soap:Envelope", entry.RequestMessage!.Body);
        Assert.Contains("Ping", entry.RequestMessage!.Body);
    }

    [Fact]
    public async Task SendAsync_Http500Fault_ThrowsSoapFaultException()
    {
        _server.Given(Request.Create().WithPath("/services/Integration").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(500)
                .WithHeader("Content-Type", "text/xml; charset=utf-8")
                .WithBody("""
                    <?xml version="1.0" encoding="utf-8"?>
                    <soap:Envelope xmlns:soap="http://schemas.xmlsoap.org/soap/envelope/">
                      <soap:Body>
                        <soap:Fault>
                          <faultcode>soap:Client</faultcode>
                          <faultstring>Geçersiz ETTN</faultstring>
                        </soap:Fault>
                      </soap:Body>
                    </soap:Envelope>
                    """));

        var transport = new SoapTransport(_http);

        var ex = await Assert.ThrowsAsync<SoapFaultException>(() => transport.SendAsync(
            new Uri(_server.Url! + "/services/Integration"),
            "http://tempuri.org/einvoice/GetStatus",
            new XElement(Svc + "GetStatus")));

        Assert.Equal("soap:Client", ex.FaultCode);
        Assert.Equal("Geçersiz ETTN", ex.FaultString);
    }
}
