using System.Globalization;
using System.Xml.Linq;
using Dental.Application.Abstractions;
using Dental.Integrations.Common;
using Microsoft.Extensions.Logging;

namespace Dental.Integrations.EInvoice.Uyumsoft;

/// <summary>
/// Uyumsoft SOAP 1.1 e-belge sürücüsü (<c>{base}/services/Integration</c>).
///
/// Sözleşme WSDL'den çıkarıldı (efatura.uyumsoft.com.tr/Services/Integration?wsdl):
/// hedef ad alanı <c>http://tempuri.org/</c>, SOAPAction <c>http://tempuri.org/IIntegration/{Op}</c>,
/// kimlik WS-Security UsernameToken (PasswordText) başlığıyla taşınır.
///
/// ÖNEMLİ: <c>SendInvoice</c> belgeyi base64/CDATA olarak DEĞİL, gövdeye gömülü TİPLİ UBL olarak alır
/// (<c>InvoiceInfo/Invoice</c> öğesinin tipi <c>urn:...:Invoice-2:InvoiceType</c>). Bu yüzden üretilen
/// UBL kök öğesinin çocukları olduğu gibi zarfın içine taşınır. (Sıkıştırılmış gönderim için
/// <c>CompressedSendInvoice</c> + BinaryRequestData ayrı bir yol; burada kullanılmıyor.)
///
/// Retry YOKTUR — yeniden deneme üst katmanda (EDocumentDispatchJob + NextAttemptAtUtc).
/// </summary>
public sealed class UyumsoftEInvoiceProvider(
    HttpClient http,
    UyumsoftSettings settings,
    ILogger<UyumsoftEInvoiceProvider> logger) : IEInvoiceProvider
{
    internal static readonly XNamespace Tempuri = "http://tempuri.org/";

    private const string ActionPrefix = "http://tempuri.org/IIntegration/";

    private readonly SoapTransport _transport = new(http);

    // ---- IEInvoiceProvider ----

    public async Task<EDocumentSendResult> SendDocumentAsync(
        EDocumentEnvelope envelope, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        EnsureSupported(envelope.DocType);

        var body = new XElement(Tempuri + "SendInvoice",
            new XElement(Tempuri + "invoices",
                BuildInvoiceInfo(envelope)));

        XElement response;
        try
        {
            response = await CallAsync("SendInvoice", body, ct).ConfigureAwait(false);
        }
        catch (SoapFaultException ex)
        {
            // İş/protokol reddi: kalıcı hata, üst katman yeniden denemeyi durum makinesine göre karar verir.
            logger.LogWarning(ex, "Uyumsoft SendInvoice SOAP Fault. Ettn={Ettn}", envelope.Ettn);
            return new EDocumentSendResult(false, Error: $"{ex.FaultCode}: {ex.FaultString}");
        }

        var result = FindResult(response, "SendInvoiceResult");
        var (succeeded, message) = ReadResponseFlags(result);
        if (!succeeded)
        {
            logger.LogWarning("Uyumsoft belge reddetti. Ettn={Ettn} Mesaj={Message}", envelope.Ettn, message);
            return new EDocumentSendResult(false, Error: message ?? "Uyumsoft belgeyi kabul etmedi.");
        }

        // InvoiceIdentity: Id (entegratör belge kimliği) + Number (GİB fatura numarası).
        var identity = result?.Elements().FirstOrDefault(e => e.Name.LocalName == "Value");
        var providerRef = identity?.Attribute("Id")?.Value ?? envelope.Ettn;
        logger.LogInformation("Uyumsoft belge kabul etti. Ettn={Ettn} Ref={Ref} No={No}",
            envelope.Ettn, providerRef, identity?.Attribute("Number")?.Value);
        return new EDocumentSendResult(true, providerRef);
    }

    public async Task<EDocumentStatusResult> GetStatusAsync(
        string documentId, EDocType type, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentId);
        EnsureSupported(type);

        var body = new XElement(Tempuri + "QueryOutboxInvoiceStatus",
            new XElement(Tempuri + "invoiceIds",
                new XElement(Tempuri + "string", documentId)));

        var response = await CallOrWrapFaultAsync("QueryOutboxInvoiceStatus", body, ct).ConfigureAwait(false);
        var result = FindResult(response, "QueryOutboxInvoiceStatusResult");
        var (succeeded, message) = ReadResponseFlags(result);
        if (!succeeded)
            return new EDocumentStatusResult(EDocProviderStatus.Unknown, message, result?.ToString());

        var value = result?.Elements().FirstOrDefault(e => e.Name.LocalName == "Value");
        if (value is null)
            return new EDocumentStatusResult(EDocProviderStatus.NotFound, message, result?.ToString());

        var status = value.Attribute("Status")?.Value ?? "";
        var detail = value.Attribute("Message")?.Value ?? message;
        return new EDocumentStatusResult(MapStatus(status), detail ?? status, value.ToString());
    }

    public async Task<byte[]> GetPdfAsync(string documentId, EDocType type, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentId);
        EnsureSupported(type);

        var body = new XElement(Tempuri + "GetOutboxInvoicePdf",
            new XElement(Tempuri + "invoiceId", documentId));

        var response = await CallOrWrapFaultAsync("GetOutboxInvoicePdf", body, ct).ConfigureAwait(false);
        var result = FindResult(response, "GetOutboxInvoicePdfResult");
        var (succeeded, message) = ReadResponseFlags(result);
        if (!succeeded)
            throw new EInvoiceProviderException(message ?? "Uyumsoft PDF döndürmedi.");

        var data = result?.Descendants().FirstOrDefault(e => e.Name.LocalName == "Data")?.Value;
        if (string.IsNullOrWhiteSpace(data))
            throw new EInvoiceProviderException("Uyumsoft PDF yanıtı boş.");

        return Convert.FromBase64String(data);
    }

    /// <summary>GİB mükellef aynası: <c>GetSystemUsersCompressedList</c> base64 zip döner.</summary>
    public async Task<Stream> DownloadGibUserListAsync(CancellationToken ct = default)
    {
        var body = new XElement(Tempuri + "GetSystemUsersCompressedList",
            new XElement(Tempuri + "type", "InvoiceReceiverbox"));

        var response = await CallOrWrapFaultAsync("GetSystemUsersCompressedList", body, ct).ConfigureAwait(false);
        var result = FindResult(response, "GetSystemUsersCompressedListResult");
        var (succeeded, message) = ReadResponseFlags(result);
        if (!succeeded)
            throw new EInvoiceProviderException(message ?? "Uyumsoft mükellef listesi döndürmedi.");

        var data = result?.Elements().FirstOrDefault(e => e.Name.LocalName == "Value")?.Value;
        if (string.IsNullOrWhiteSpace(data))
            throw new EInvoiceProviderException("Uyumsoft mükellef listesi yanıtı boş.");

        return new MemoryStream(Convert.FromBase64String(data));
    }

    public async Task CancelEArchiveAsync(string documentId, string reason, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentId);

        // İptal gerekçesi Uyumsoft sözleşmesinde taşınmıyor (yalnız InvoiceId + CancelDate);
        // gerekçe yerel InvoiceStatusLog'a yazılır.
        var body = new XElement(Tempuri + "CancelEArchiveInvoice",
            new XElement(Tempuri + "request",
                new XAttribute("InvoiceId", documentId),
                new XAttribute("CancelDate", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture))));

        var response = await CallOrWrapFaultAsync("CancelEArchiveInvoice", body, ct).ConfigureAwait(false);
        var result = FindResult(response, "CancelEArchiveInvoiceResult");
        var (succeeded, message) = ReadResponseFlags(result);
        if (!succeeded)
            throw new EInvoiceProviderException(message ?? "Uyumsoft e-Arşiv iptalini reddetti.");

        logger.LogInformation("Uyumsoft e-Arşiv iptali kabul edildi. Ref={Ref} Gerekçe={Reason}", documentId, reason);
    }

    // ---- Zarf kurulumu ----

    /// <summary>InvoiceInfo öğesi; şema sırası: Invoice, TargetCustomer, EArchiveInvoiceInfo, Scenario, CreateDateUtc.</summary>
    internal static XElement BuildInvoiceInfo(EDocumentEnvelope envelope)
    {
        var ubl = ParseUbl(envelope.UblXml);

        var invoiceInfo = new XElement(Tempuri + "InvoiceInfo",
            new XAttribute("LocalDocumentId", envelope.Ettn));

        // Tipli UBL: kök öğenin çocukları (cbc:/cac:) olduğu gibi taşınır.
        var invoice = new XElement(Tempuri + "Invoice");
        foreach (var child in ubl.Root!.Elements())
            invoice.Add(new XElement(child));
        invoiceInfo.Add(invoice);

        if (envelope.DocType == EDocType.EInvoice)
        {
            var customer = new XElement(Tempuri + "TargetCustomer",
                new XAttribute("VknTckn", ReadBuyerTaxId(ubl) ?? ""));
            if (!string.IsNullOrWhiteSpace(envelope.TargetAlias))
                customer.Add(new XAttribute("Alias", envelope.TargetAlias));
            invoiceInfo.Add(customer);
        }
        else
        {
            // e-Arşiv: kağıt/elektronik teslim şekli belgeye değil zarfa yazılır.
            invoiceInfo.Add(new XElement(Tempuri + "EArchiveInvoiceInfo",
                new XAttribute("DeliveryType",
                    envelope.SendMode == EDocSendMode.Kagit ? "Paper" : "Electronic")));
        }

        invoiceInfo.Add(new XElement(Tempuri + "Scenario",
            envelope.DocType == EDocType.EInvoice ? "eInvoice" : "eArchive"));
        invoiceInfo.Add(new XElement(Tempuri + "CreateDateUtc",
            DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture)));
        return invoiceInfo;
    }

    /// <summary>WS-Security UsernameToken (PasswordText) başlığı — SYS sürücüsüyle ortak.</summary>
    internal static XElement BuildSecurityHeader(string username, string password) =>
        WsSecurity.UsernameToken(username, password);

    /// <summary>
    /// Taşıma hatasını portun sözleşmesine (<see cref="EInvoiceProviderException"/>) çevirir —
    /// üst katman sürücüye özgü istisna tiplerini bilmez, bilmemelidir. SOAP Fault (iş reddi)
    /// bilerek dışarı sızar; gönderim yolunda "kabul edilmedi" olarak yorumlanır.
    /// </summary>
    private async Task<XElement> CallAsync(string operation, XElement body, CancellationToken ct)
    {
        try
        {
            return await _transport.SendAsync(
                new Uri(settings.EndpointUrl),
                ActionPrefix + operation,
                body,
                [BuildSecurityHeader(settings.Username, settings.Password)],
                ct).ConfigureAwait(false);
        }
        catch (SoapTransportException ex)
        {
            throw new EInvoiceProviderException(
                $"Uyumsoft {operation} çağrısı başarısız ({settings.EndpointUrl}): {ex.Message}", ex);
        }
    }

    /// <summary>Fault'u da porta çeviren sarmalayıcı: sorgu/iptal uçlarında Fault iş reddidir.</summary>
    private async Task<XElement> CallOrWrapFaultAsync(string operation, XElement body, CancellationToken ct)
    {
        try
        {
            return await CallAsync(operation, body, ct).ConfigureAwait(false);
        }
        catch (SoapFaultException ex)
        {
            throw new EInvoiceProviderException(
                $"Uyumsoft {operation} reddetti [{ex.FaultCode}]: {ex.FaultString}", ex);
        }
    }

    // ---- Yardımcılar ----

    private void EnsureSupported(EDocType type)
    {
        if (type != EDocType.ESmm) return;

        // Integration servisi yalnız UBL Invoice taşır; e-SMM (CreditNote) ayrı uçtan yürür.
        // Uç adresi doğrulanana kadar sessizce yanlış belge göndermek yerine açıkça reddedilir.
        throw new EInvoiceProviderException(
            "Uyumsoft Integration servisi e-SMM (CreditNote) taşımaz; ayrı e-SMM ucu (Settings.SmmUrl) " +
            "tanımlanmadan e-Serbest Meslek Makbuzu gönderilemez.");
    }

    private static XDocument ParseUbl(string ublXml)
    {
        try
        {
            return XDocument.Parse(ublXml);
        }
        catch (System.Xml.XmlException ex)
        {
            throw new EInvoiceProviderException("Gönderilecek UBL belgesi XML olarak çözümlenemedi.", ex);
        }
    }

    /// <summary>Alıcı VKN/TCKN'yi UBL'den okur (AccountingCustomerParty/Party/PartyIdentification/ID).</summary>
    private static string? ReadBuyerTaxId(XDocument ubl) =>
        ubl.Descendants().FirstOrDefault(e => e.Name.LocalName == "AccountingCustomerParty")
            ?.Descendants().FirstOrDefault(e =>
                e.Name.LocalName == "ID" &&
                e.Attribute("schemeID")?.Value is "VKN" or "TCKN")
            ?.Value;

    private static XElement? FindResult(XElement response, string name) =>
        response.Name.LocalName == name
            ? response
            : response.Descendants().FirstOrDefault(e => e.Name.LocalName == name);

    private static (bool Succeeded, string? Message) ReadResponseFlags(XElement? result)
    {
        if (result is null) return (false, "Uyumsoft yanıtı beklenen sonucu içermiyor.");
        var flag = result.Attribute("IsSucceded")?.Value;
        var message = result.Attribute("Message")?.Value;
        var succeeded = bool.TryParse(flag, out var parsed) && parsed;
        return (succeeded, string.IsNullOrWhiteSpace(message) ? null : message);
    }

    /// <summary>Uyumsoft InvoiceStatus → portun sağlayıcı-bağımsız durumu.</summary>
    internal static EDocProviderStatus MapStatus(string status) => status switch
    {
        "NotPrepared" or "NotSend" or "Draft" or "Queued" => EDocProviderStatus.Queued,
        "Processing" or "SentToGib" or "WaitingForAprovement" => EDocProviderStatus.Processing,
        "Approved" => EDocProviderStatus.Succeeded,
        // Alıcının TİCARİ senaryoda reddi / iadesi.
        "Declined" or "Return" => EDocProviderStatus.BuyerRejected,
        "Canceled" or "EArchivedCanceled" => EDocProviderStatus.Cancelled,
        "Error" => EDocProviderStatus.GibRejected,
        _ => EDocProviderStatus.Unknown,
    };
}
