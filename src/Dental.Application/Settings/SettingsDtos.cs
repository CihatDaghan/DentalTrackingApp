using Dental.Domain.Enums;

namespace Dental.Application.Settings;

// ---- Klinik ayarları ----

/// <summary>
/// Klinik künyesi + kiracı vergi kimliği. <see cref="LegalType"/> e-belge karar motorunu besler
/// (şahıs hekim → e-SMM, şirket → e-Fatura/e-Arşiv).
/// </summary>
public sealed record ClinicSettingsDto(
    long TenantId,
    string TenantName,
    TenantLegalType LegalType,
    string? TaxNumber,
    string? TaxOffice,
    bool HasHealthTourismAuthorization,
    string DefaultLocale,
    TenantStatus Status,
    string? PlanCode,
    DateTime? TrialEndsAtUtc,
    long ClinicId,
    string ClinicName,
    string? Address,
    string? City,
    string? District,
    string? Phone,
    string? Email,
    string? CkysCode,
    long? LogoFileId);

public sealed record ClinicSettingsUpdateRequest(
    string TenantName,
    TenantLegalType LegalType,
    string ClinicName,
    string? TaxNumber = null,
    string? TaxOffice = null,
    bool HasHealthTourismAuthorization = false,
    string? Address = null,
    string? City = null,
    string? District = null,
    string? Phone = null,
    string? Email = null,
    string? CkysCode = null,
    long? LogoFileId = null,
    /// <summary>NULL ise bağlamdaki (ya da kiracının ilk) klinik güncellenir.</summary>
    long? ClinicId = null);

// ---- Klinik çalışma saatleri ----

public sealed record ClinicWorkingHourDto(
    long Id,
    long ClinicId,
    DayOfWeek DayOfWeek,
    TimeOnly? OpenTime,
    TimeOnly? CloseTime,
    bool IsClosed);

public sealed record ClinicWorkingHourItem(DayOfWeek DayOfWeek, TimeOnly? OpenTime, TimeOnly? CloseTime, bool IsClosed);

public sealed record ClinicWorkingHoursSaveRequest(long ClinicId, IReadOnlyList<ClinicWorkingHourItem> Items);

// ---- Personel ----

public sealed record StaffRoleDto(long Id, string Name, bool IsSystem);

public sealed record StaffDto(
    long Id,
    string Email,
    string FirstName,
    string LastName,
    string FullName,
    UserType UserType,
    bool IsActive,
    bool MustChangePassword,
    string? Color,
    string? Branch,
    string? DiplomaNo,
    IReadOnlyList<StaffRoleDto> Roles,
    IReadOnlyList<long> ClinicIds,
    DateTime? LastLoginUtc,
    DateTime CreatedAtUtc);

public sealed record StaffInviteRequest(
    string Email,
    string FirstName,
    string LastName,
    UserType UserType,
    IReadOnlyList<long> RoleIds,
    long? ClinicId = null,
    string? Color = null,
    string? Branch = null,
    string? DiplomaNo = null);

/// <summary>Davet sonucu: geçici şifre YALNIZ bu yanıtta döner, saklanmaz.</summary>
public sealed record StaffInviteResultDto(StaffDto User, string TemporaryPassword);

public sealed record StaffUpdateRequest(
    string FirstName,
    string LastName,
    UserType UserType,
    IReadOnlyList<long> RoleIds,
    bool IsActive = true,
    string? Color = null,
    string? Branch = null,
    string? DiplomaNo = null);

public sealed record TemporaryPasswordDto(string TemporaryPassword);

// ---- Yetki matrisi ----

public sealed record RolePermissionsDto(
    long Id,
    string Name,
    bool IsSystem,
    IReadOnlyList<string> Permissions,
    int UserCount);

public sealed record RolePermissionsUpdateRequest(IReadOnlyList<string> Permissions);

/// <summary>İzin kataloğunun modül kırılımı — yetki matrisi ekranı bunu çizer.</summary>
public sealed record PermissionCatalogDto(IReadOnlyDictionary<string, IReadOnlyList<string>> ByModule);

// ---- Entegrasyon ayarları ----

/// <summary>
/// Kiracı entegrasyon ayarı. Sırlar YAZMA-TEK-YÖNLÜDÜR: yanıtta yalnız maskeli
/// (<c>••••1234</c>) hâlleri döner, düz metin hiçbir zaman geri okunmaz.
/// </summary>
public sealed record IntegrationSettingDto(
    string IntegrationKey,
    string ProviderKey,
    string Environment,
    bool IsEnabled,
    IReadOnlyDictionary<string, string?> Settings,
    IReadOnlyList<string> SecretFields,
    IReadOnlyList<string> AvailableProviders,
    DateTime? UpdatedAtUtc,
    long? UpdatedByUserId);

public sealed record IntegrationSettingUpdateRequest(
    string ProviderKey,
    string Environment,
    bool IsEnabled,
    /// <summary>Maskeli değer (••••1234) gönderilirse mevcut sır korunur.</summary>
    IReadOnlyDictionary<string, string?> Settings);

public sealed record IntegrationTestResultDto(bool Success, string Message, int DurationMs, string ProviderKey);

/// <summary>Kiracı yöneticisinin ayar ekranı: klinik künyesi, personel, roller, entegrasyonlar.</summary>
public interface ISettingsService
{
    Task<ClinicSettingsDto> GetClinicAsync(CancellationToken ct = default);
    Task<ClinicSettingsDto> UpdateClinicAsync(ClinicSettingsUpdateRequest request, CancellationToken ct = default);

    Task<IReadOnlyList<ClinicWorkingHourDto>> GetClinicWorkingHoursAsync(long? clinicId, CancellationToken ct = default);
    Task<IReadOnlyList<ClinicWorkingHourDto>> SaveClinicWorkingHoursAsync(
        ClinicWorkingHoursSaveRequest request, CancellationToken ct = default);

    Task<IReadOnlyList<StaffDto>> ListStaffAsync(bool includeInactive, CancellationToken ct = default);
    Task<StaffDto> GetStaffAsync(long id, CancellationToken ct = default);
    Task<StaffInviteResultDto> InviteStaffAsync(StaffInviteRequest request, CancellationToken ct = default);
    Task<StaffDto> UpdateStaffAsync(long id, StaffUpdateRequest request, CancellationToken ct = default);
    Task<TemporaryPasswordDto> ResetStaffPasswordAsync(long id, CancellationToken ct = default);
    /// <summary>Pasife alır (hard delete yok). Kendini ve son aktif Owner'ı pasife alamaz.</summary>
    Task DeactivateStaffAsync(long id, CancellationToken ct = default);

    Task<IReadOnlyList<RolePermissionsDto>> ListRolesAsync(CancellationToken ct = default);
    Task<RolePermissionsDto> UpdateRolePermissionsAsync(
        long roleId, RolePermissionsUpdateRequest request, CancellationToken ct = default);
    PermissionCatalogDto GetPermissionCatalog();

    Task<IReadOnlyList<IntegrationSettingDto>> ListIntegrationsAsync(CancellationToken ct = default);
    Task<IntegrationSettingDto> UpdateIntegrationAsync(
        string integrationKey, IntegrationSettingUpdateRequest request, CancellationToken ct = default);
    Task<IntegrationTestResultDto> TestIntegrationAsync(string integrationKey, CancellationToken ct = default);
}
