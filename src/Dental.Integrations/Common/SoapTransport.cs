using System.Text;
using System.Xml.Linq;

namespace Dental.Integrations.Common;

/// <summary>
/// SOAP 1.1 (document/literal) taşıma katmanı: zarf oluşturma, SOAPAction'lı POST, yanıt zarfını
/// açma ve Fault çözümleme. Uyumsoft (e-belge) ve SYS (e-Nabız) sürücülerinin ortak temelidir.
/// Retry yoktur; retry üst katmanındır.
/// </summary>
public sealed class SoapTransport(HttpClient http)
{
    internal static readonly XNamespace SoapNs = "http://schemas.xmlsoap.org/soap/envelope/";

    /// <summary>SOAP 1.1 zarfı üretir; başlık içeriği yoksa Header öğesi hiç yazılmaz.</summary>
    public static XDocument BuildEnvelope(XElement bodyContent, IEnumerable<XElement>? headerContent = null)
    {
        var envelope = new XElement(SoapNs + "Envelope", new XAttribute(XNamespace.Xmlns + "soap", SoapNs));
        var headers = headerContent?.ToArray();
        if (headers is { Length: > 0 })
            envelope.Add(new XElement(SoapNs + "Header", headers));
        envelope.Add(new XElement(SoapNs + "Body", bodyContent));
        return new XDocument(new XDeclaration("1.0", "utf-8", null), envelope);
    }

    /// <summary>
    /// Yanıt zarfını açar ve Body'nin ilk öğesini döner.
    /// </summary>
    /// <exception cref="SoapFaultException">Body bir Fault içeriyorsa.</exception>
    /// <exception cref="SoapTransportException">Gövde XML değilse veya zarf yapısı beklenen gibi değilse.</exception>
    public static XElement UnwrapBody(string responseXml)
    {
        XDocument doc;
        try
        {
            doc = XDocument.Parse(responseXml);
        }
        catch (System.Xml.XmlException ex)
        {
            throw new SoapTransportException($"SOAP yanıtı XML olarak çözümlenemedi: {ex.Message}", ex);
        }

        var body = doc.Root?.Element(SoapNs + "Body")
            ?? throw new SoapTransportException("SOAP yanıtında Body öğesi bulunamadı.");

        var fault = body.Element(SoapNs + "Fault") ?? body.Element("Fault");
        if (fault is not null)
        {
            // SOAP 1.1: faultcode/faultstring nitelenmemiş (unqualified) öğelerdir.
            var faultCode = fault.Element("faultcode")?.Value ?? "";
            var faultString = fault.Element("faultstring")?.Value ?? "";
            var detail = fault.Element("detail")?.ToString();
            throw new SoapFaultException(faultCode, faultString, detail);
        }

        return body.Elements().FirstOrDefault()
            ?? throw new SoapTransportException("SOAP yanıtının Body öğesi boş.");
    }

    /// <summary>Zarfı POST eder, yanıt Body'sinin ilk öğesini döner (Fault ise fırlatır).</summary>
    public async Task<XElement> SendAsync(
        Uri endpoint,
        string soapAction,
        XElement bodyContent,
        IEnumerable<XElement>? headerContent = null,
        CancellationToken ct = default)
    {
        var envelope = BuildEnvelope(bodyContent, headerContent);
        var xml = envelope.Declaration + Environment.NewLine + envelope.ToString(SaveOptions.DisableFormatting);

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(xml, Encoding.UTF8, "text/xml"),
        };
        request.Headers.TryAddWithoutValidation("SOAPAction", $"\"{soapAction}\"");

        string responseXml;
        try
        {
            // Fault'lar tipik olarak HTTP 500 ile döner; durum kodundan bağımsız gövde okunur.
            using var response = await http.SendAsync(request, ct).ConfigureAwait(false);
            responseXml = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(responseXml))
                throw new SoapTransportException($"SOAP yanıtı boş (HTTP {(int)response.StatusCode}).");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException ex)
        {
            throw new SoapTransportException("SOAP isteği zaman aşımına uğradı.", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new SoapTransportException($"SOAP isteği başarısız: {ex.Message}", ex);
        }

        return UnwrapBody(responseXml);
    }

    public Task<XElement> SendAsync(string endpointUrl, string soapAction, XElement bodyContent, CancellationToken ct = default)
        => SendAsync(new Uri(endpointUrl), soapAction, bodyContent, headerContent: null, ct);
}

/// <summary>Sunucunun döndürdüğü SOAP Fault (iş/protokol reddi).</summary>
public sealed class SoapFaultException(string faultCode, string faultString, string? detail = null)
    : Exception($"SOAP Fault [{faultCode}]: {faultString}")
{
    public string FaultCode { get; } = faultCode;
    public string FaultString { get; } = faultString;
    public string? Detail { get; } = detail;
}

/// <summary>Taşıma katmanı hatası (ağ, zaman aşımı, bozuk zarf).</summary>
public sealed class SoapTransportException : Exception
{
    public SoapTransportException(string message) : base(message) { }
    public SoapTransportException(string message, Exception innerException) : base(message, innerException) { }
}
