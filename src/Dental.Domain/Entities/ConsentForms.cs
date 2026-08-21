using Dental.Domain.Common;
using Dental.Domain.Enums;

namespace Dental.Domain.Entities;

/// <summary>
/// Onam şablonu. BodyHtml sunucu tarafında sanitize edilerek saklanır (denetim (c)-7);
/// yer tutucular: {{HastaAdi}} {{HekimAdi}} {{KlinikAdi}} {{Tarih}} {{Tedavi}}.
/// Gövde değiştiğinde Version artar; imzalı formlar TemplateVersion snapshot'ı taşır.
/// </summary>
public class ConsentTemplate : TenantEntity
{
    public required string Name { get; set; }
    public required string BodyHtml { get; set; }
    public string Locale { get; set; } = "tr";
    public int Version { get; set; } = 1;
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Hastaya üretilmiş onam formu. RenderedHtml doldurulmuş halidir — şablon sonradan
/// değişse de imzalanan metin kanıt olarak sabit kalır. SMS akışı: SignToken'lı public
/// link → hasta imzalar → PDF üretilir → MediaFile (ConsentPdf). Token tek kullanımlıktır.
/// </summary>
public class ConsentForm : TenantEntity
{
    public long ClinicId { get; set; }
    public long PatientId { get; set; }
    public long TemplateId { get; set; }
    public int TemplateVersion { get; set; }
    public long? TreatmentRecordId { get; set; }
    public required string RenderedHtml { get; set; }
    public ConsentFormStatus Status { get; set; } = ConsentFormStatus.Draft;
    public ConsentSignChannel? SignChannel { get; set; }
    /// <summary>Public imza linki token'ı (UQ). send-sms'te yenilenir.</summary>
    public Guid SignToken { get; set; }
    public DateTime? SignTokenExpiresAtUtc { get; set; }
    public DateTime? SignedAtUtc { get; set; }
    public string? SignerIp { get; set; }
    public string? SignerUserAgent { get; set; }
    /// <summary>İmza görüntüsü (MediaFile, SignatureImage).</summary>
    public long? SignatureFileId { get; set; }
    /// <summary>İmzalı nihai PDF (MediaFile, ConsentPdf).</summary>
    public long? PdfFileId { get; set; }
    /// <summary>PDF bütünlük özeti (hex).</summary>
    public string? PdfSha256 { get; set; }

    public ConsentTemplate? Template { get; set; }
}
