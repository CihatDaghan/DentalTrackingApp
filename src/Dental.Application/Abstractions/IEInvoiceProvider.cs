namespace Dental.Application.Abstractions;

public enum EDocType
{
    /// <summary>e-Fatura (alıcı GİB mükellef aynasında kayıtlı).</summary>
    EInvoice = 0,
    /// <summary>e-Arşiv fatura (kayıtsız alıcı / bireysel).</summary>
    EArchive = 1,
    /// <summary>e-Serbest Meslek Makbuzu (muayenehane / serbest meslek).</summary>
    ESmm = 2
}

/// <summary>e-Arşiv teslim şekli.</summary>
public enum EDocSendMode
{
    Elektronik = 0,
    Kagit = 1
}

/// <param name="UblXml">UBL-TR 1.2 belge XML'i (Dental.EDocument.Ubl üretir; sürücü içeriğe dokunmaz).</param>
/// <param name="Ettn">Belgenin UUID'i (ETTN).</param>
/// <param name="TargetAlias">e-Faturada alıcının posta kutusu etiketi (urn:mail:...); e-Arşiv/e-SMM'de null.</param>
public sealed record EDocumentEnvelope(
    EDocType DocType,
    string UblXml,
    string Ettn,
    string? TargetAlias = null,
    EDocSendMode SendMode = EDocSendMode.Elektronik);

public sealed record EDocumentSendResult(
    bool Success,
    string? ProviderRef = null,
    string? Error = null);

/// <summary>Sağlayıcıdan sorgulanan belge durumu (EDocuments.Status geçişlerini üst katman yapar).</summary>
public enum EDocProviderStatus
{
    Unknown = 0,
    Queued = 1,
    Processing = 2,
    Succeeded = 3,
    GibRejected = 4,
    /// <summary>TİCARİ senaryoda alıcı reddi.</summary>
    BuyerRejected = 5,
    Cancelled = 6,
    NotFound = 7
}

public sealed record EDocumentStatusResult(
    EDocProviderStatus Status,
    string? StatusDetail = null,
    string? RawResponse = null);

/// <summary>
/// e-Belge (e-Fatura / e-Arşiv / e-SMM) entegratör portu.
/// Sürücüler (Uyumsoft SOAP, NES REST) Dental.Integrations/EInvoice altında yaşar; retry üst katmanındır.
/// </summary>
public interface IEInvoiceProvider
{
    Task<EDocumentSendResult> SendDocumentAsync(EDocumentEnvelope envelope, CancellationToken ct = default);
    Task<EDocumentStatusResult> GetStatusAsync(string ettn, EDocType type, CancellationToken ct = default);
    Task<byte[]> GetPdfAsync(string ettn, EDocType type, CancellationToken ct = default);
    /// <summary>GİB mükellef aynası günlük zip listesi (GibTaxpayers senkronu için).</summary>
    Task<Stream> DownloadGibUserListAsync(CancellationToken ct = default);
    Task CancelEArchiveAsync(string ettn, string reason, CancellationToken ct = default);
}

public sealed class EInvoiceProviderException : Exception
{
    public EInvoiceProviderException(string message) : base(message) { }
    public EInvoiceProviderException(string message, Exception innerException) : base(message, innerException) { }
}
