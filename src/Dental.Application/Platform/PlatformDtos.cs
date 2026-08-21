using Dental.Domain.Enums;

namespace Dental.Application.Platform;

// ---- Plan ----

public sealed record PlanDto(
    long Id,
    string Code,
    string Name,
    int MaxUsers,
    int MaxPatients,
    int MonthlySmsQuota,
    int StorageGb,
    decimal PriceMonthly,
    bool IsActive,
    int SortOrder,
    /// <summary>Bu planı kullanan kiracı sayısı.</summary>
    int TenantCount);

public sealed record PlanUpsertRequest(
    string Code,
    string Name,
    int MaxUsers,
    int MaxPatients,
    int MonthlySmsQuota,
    int StorageGb,
    decimal PriceMonthly,
    bool IsActive = true,
    int SortOrder = 0);

// ---- Duyuru ----

public sealed record AnnouncementDto(
    long Id,
    string Title,
    string Body,
    AnnouncementSeverity Severity,
    DateTime StartsAtUtc,
    DateTime? EndsAtUtc,
    bool IsActive,
    long? TargetTenantId,
    string? TargetTenantName,
    DateTime CreatedAtUtc);

public sealed record AnnouncementUpsertRequest(
    string Title,
    string Body,
    AnnouncementSeverity Severity = AnnouncementSeverity.Info,
    /// <summary>NULL ise şimdi (UTC).</summary>
    DateTime? StartsAtUtc = null,
    DateTime? EndsAtUtc = null,
    bool IsActive = true,
    /// <summary>NULL = tüm kiracılar.</summary>
    long? TargetTenantId = null);

/// <summary>Uygulama içi banner için sadeleştirilmiş duyuru.</summary>
public sealed record ActiveAnnouncementDto(
    long Id,
    string Title,
    string Body,
    AnnouncementSeverity Severity,
    DateTime StartsAtUtc,
    DateTime? EndsAtUtc);

// ---- Kiracı yönetimi ----

public sealed record TenantListQuery(
    string? Search = null,
    TenantStatus? Status = null,
    string? PlanCode = null,
    bool IncludeDeleted = false,
    int Page = 1,
    int PageSize = 25);

public sealed record TenantUsageDto(
    int UserCount,
    int PatientCount,
    int AppointmentCount,
    int InvoiceCount,
    int TreatmentCount,
    DateTime? LastActivityUtc);

public sealed record TenantListItemDto(
    long Id,
    string Name,
    TenantLegalType LegalType,
    TenantStatus Status,
    string? PlanCode,
    string? PlanName,
    DateTime CreatedAtUtc,
    DateTime? TrialEndsAtUtc,
    bool IsDeleted,
    TenantUsageDto Usage);

public sealed record TenantClinicDto(long Id, string Name, string? City, string? Phone, string? CkysCode);

public sealed record TenantOwnerDto(long Id, string Email, string FullName, bool IsActive);

public sealed record TenantDetailDto(
    long Id,
    string Name,
    TenantLegalType LegalType,
    string? TaxNumber,
    string? TaxOffice,
    bool HasHealthTourismAuthorization,
    TenantStatus Status,
    string? PlanCode,
    string? PlanName,
    DateTime CreatedAtUtc,
    DateTime? TrialEndsAtUtc,
    bool IsDeleted,
    TenantUsageDto Usage,
    IReadOnlyList<TenantClinicDto> Clinics,
    IReadOnlyList<TenantOwnerDto> Owners);

public sealed record TenantUpdateRequest(
    string? Name = null,
    string? PlanCode = null,
    TenantStatus? Status = null,
    DateTime? TrialEndsAtUtc = null);

/// <summary>
/// Audit'li kimliğe bürünme sonucu. Kısa ömürlü (15 dk) access token; refresh token
/// ÜRETİLMEZ — oturum uzatılamaz, süre dolunca yeniden süper admin onayı gerekir.
/// </summary>
public sealed record ImpersonationResponse(
    string AccessToken,
    int ExpiresInSeconds,
    DateTime ExpiresAtUtc,
    long TenantId,
    string TenantName,
    long ImpersonatedUserId,
    string ImpersonatedUserEmail,
    long AuditLogId);

// ---- Entegrasyon sağlığı ----

public sealed record IntegrationHealthRowDto(
    string IntegrationKey,
    string? ProviderKey,
    string Environment,
    bool IsEnabled,
    bool HasCredentials,
    DateTime? LastSuccessUtc,
    DateTime? LastFailureUtc,
    int CallCount24h,
    int FailureCount24h,
    string? LastError);

public sealed record TenantIntegrationHealthDto(
    long TenantId,
    string TenantName,
    TenantStatus Status,
    IReadOnlyList<IntegrationHealthRowDto> Integrations,
    EnabizMode EnabizMode,
    EnabizMode EnabizRequestedMode,
    bool KtsRegistered);

/// <summary>Süper admin (platform) yönetimi. Tüm uçlar SuperAdmin politikasına bağlıdır.</summary>
public interface IPlatformAdminService
{
    // Plan
    Task<IReadOnlyList<PlanDto>> ListPlansAsync(bool includeInactive, CancellationToken ct = default);
    Task<PlanDto> GetPlanAsync(long id, CancellationToken ct = default);
    Task<PlanDto> CreatePlanAsync(PlanUpsertRequest request, CancellationToken ct = default);
    Task<PlanDto> UpdatePlanAsync(long id, PlanUpsertRequest request, CancellationToken ct = default);
    Task DeletePlanAsync(long id, CancellationToken ct = default);

    // Duyuru
    Task<IReadOnlyList<AnnouncementDto>> ListAnnouncementsAsync(CancellationToken ct = default);
    Task<AnnouncementDto> GetAnnouncementAsync(long id, CancellationToken ct = default);
    Task<AnnouncementDto> CreateAnnouncementAsync(AnnouncementUpsertRequest request, CancellationToken ct = default);
    Task<AnnouncementDto> UpdateAnnouncementAsync(long id, AnnouncementUpsertRequest request, CancellationToken ct = default);
    Task DeleteAnnouncementAsync(long id, CancellationToken ct = default);

    // Kiracı
    Task<Common.PagedResult<TenantListItemDto>> ListTenantsAsync(TenantListQuery query, CancellationToken ct = default);
    Task<TenantDetailDto> GetTenantAsync(long id, CancellationToken ct = default);
    Task<TenantDetailDto> UpdateTenantAsync(long id, TenantUpdateRequest request, CancellationToken ct = default);
    /// <summary>Soft delete (kiracı askıya alınır ve silinmiş işaretlenir); onay parametresi zorunludur.</summary>
    Task DeleteTenantAsync(long id, bool confirm, CancellationToken ct = default);

    /// <summary>Hedef kiracının Owner'ı adına 15 dk ömürlü token üretir + AuditLog (Impersonation) yazar.</summary>
    Task<ImpersonationResponse> ImpersonateAsync(long tenantId, string? ip, string? userAgent, CancellationToken ct = default);

    Task<IReadOnlyList<TenantIntegrationHealthDto>> GetIntegrationHealthAsync(
        long? tenantId, CancellationToken ct = default);

    /// <summary>Kiracıya (ve genel duyurulara) açık, penceresi geçerli duyurular.</summary>
    Task<IReadOnlyList<ActiveAnnouncementDto>> GetActiveAnnouncementsAsync(
        long? tenantId, CancellationToken ct = default);
}
