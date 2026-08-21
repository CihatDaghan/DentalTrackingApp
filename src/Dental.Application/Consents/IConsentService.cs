using Dental.Application.Media;

namespace Dental.Application.Consents;

public interface IConsentService
{
    // ---- Şablonlar (BodyHtml her kayıtta sunucu tarafında sanitize edilir) ----
    Task<IReadOnlyList<ConsentTemplateListItemDto>> ListTemplatesAsync(bool includeInactive = false, CancellationToken ct = default);
    Task<ConsentTemplateDto> GetTemplateAsync(long id, CancellationToken ct = default);
    Task<ConsentTemplateDto> CreateTemplateAsync(ConsentTemplateUpsertRequest request, CancellationToken ct = default);
    /// <summary>BodyHtml değişmişse Version artar.</summary>
    Task<ConsentTemplateDto> UpdateTemplateAsync(long id, ConsentTemplateUpsertRequest request, CancellationToken ct = default);
    Task DeleteTemplateAsync(long id, CancellationToken ct = default);

    // ---- Formlar ----
    /// <summary>Şablon + hasta (+ opsiyonel tedavi) → yer tutucular doldurulur, RenderedHtml sabitlenir, Status=Draft.</summary>
    Task<ConsentFormDto> CreateAsync(long patientId, ConsentCreateRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<ConsentFormDto>> ListForPatientAsync(long patientId, CancellationToken ct = default);
    Task<ConsentFormDto> GetAsync(long id, CancellationToken ct = default);
    /// <summary>Klinik içi tablet imzası: imza PNG → MediaFile → PDF üretimi → Status=Signed.</summary>
    Task<ConsentFormDto> SignTabletAsync(long id, ConsentSignRequest request, string? signerIp, string? signerUserAgent, CancellationToken ct = default);
    /// <summary>SignToken yenilenir (72 saat), public link SMS ile gönderilir, Status=SentBySms.</summary>
    Task<ConsentSendSmsResult> SendSmsAsync(long id, CancellationToken ct = default);
    Task<MediaDownload> OpenPdfAsync(long id, CancellationToken ct = default);

    // ---- Public akış (PublicConsentService'in kurduğu tenant scope İÇİNDEN çağrılır) ----
    Task<PublicConsentViewDto> GetPublicViewAsync(long formId, CancellationToken ct = default);
    Task<PublicConsentViewDto> SignPublicAsync(long formId, PublicConsentSignRequest request, string? signerIp, string? signerUserAgent, CancellationToken ct = default);
}

/// <summary>
/// Anonim (token'lı) uçların servisi. Denetim (c)-6: token→tenant çözümlemesi IgnoreQueryFilters
/// ile yapılır, ardından ITenantScopeFactory ile tenant bağlamlı scope kurulup iş oradan yürütülür.
/// </summary>
public interface IPublicConsentService
{
    Task<PublicConsentViewDto> GetByTokenAsync(Guid token, CancellationToken ct = default);
    Task<PublicConsentViewDto> SignByTokenAsync(Guid token, PublicConsentSignRequest request, string? signerIp, string? signerUserAgent, CancellationToken ct = default);
}
