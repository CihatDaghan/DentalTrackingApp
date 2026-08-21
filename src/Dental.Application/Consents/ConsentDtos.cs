using Dental.Domain.Enums;

namespace Dental.Application.Consents;

// ---- Şablonlar ----

public sealed record ConsentTemplateUpsertRequest(
    string Name,
    string BodyHtml,
    string Locale = "tr",
    bool IsActive = true);

public sealed record ConsentTemplateDto(
    long Id,
    string Name,
    string BodyHtml,
    string Locale,
    int Version,
    bool IsActive);

public sealed record ConsentTemplateListItemDto(long Id, string Name, string Locale, int Version, bool IsActive);

// ---- Formlar ----

public sealed record ConsentCreateRequest(long TemplateId, long? TreatmentRecordId = null);

public sealed record ConsentFormDto(
    long Id,
    long PatientId,
    string PatientName,
    long TemplateId,
    string TemplateName,
    int TemplateVersion,
    long? TreatmentRecordId,
    string RenderedHtml,
    ConsentFormStatus Status,
    ConsentSignChannel? SignChannel,
    DateTime? SignTokenExpiresAtUtc,
    DateTime? SignedAtUtc,
    long? SignatureFileId,
    long? PdfFileId,
    string? PdfSha256,
    DateTime CreatedAtUtc);

/// <summary>Tablet (klinik içi, auth'lu) imza isteği.</summary>
public sealed record ConsentSignRequest(string SignaturePngBase64);

public sealed record ConsentSendSmsResult(
    long ConsentId,
    string PublicUrl,
    DateTime ExpiresAtUtc,
    string SentToPhone);

// ---- Public (token'lı) akış ----

/// <summary>Public sayfa görünümü; hasta adı KVKK gereği maskelidir (A*** Y***).</summary>
public sealed record PublicConsentViewDto(
    string ClinicName,
    string PatientMaskedName,
    string RenderedHtml,
    ConsentFormStatus Status,
    string Locale);

public sealed record PublicConsentSignRequest(string? SignaturePngBase64 = null, bool Declined = false);

/// <summary>Token tek kullanımlık/süreli: imzalanmış, reddedilmiş veya süresi dolmuş linkler 410 Gone döner.</summary>
public sealed class ConsentGoneException(string message) : Exception(message);
