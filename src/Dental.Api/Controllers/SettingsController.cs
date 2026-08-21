using Dental.Api.Auth;
using Dental.Application.Settings;
using Microsoft.AspNetCore.Mvc;

namespace Dental.Api.Controllers;

/// <summary>
/// Kiracı yöneticisinin ayar ekranı: klinik künyesi, çalışma saatleri, personel,
/// yetki matrisi ve entegrasyon kimlik bilgileri.
/// </summary>
[ApiController]
[Route("api/v1/settings")]
public sealed class SettingsController(ISettingsService settings) : ControllerBase
{
    // ---- Klinik künyesi ----

    [HttpGet("clinic")]
    [HasPermission("settings.view")]
    public async Task<ActionResult<ClinicSettingsDto>> GetClinic(CancellationToken ct)
        => Ok(await settings.GetClinicAsync(ct));

    /// <summary>LegalType e-belge karar motorunu besler (şahıs → e-SMM, şirket → e-Fatura/e-Arşiv).</summary>
    [HttpPut("clinic")]
    [HasPermission("settings.update")]
    public async Task<ActionResult<ClinicSettingsDto>> UpdateClinic(
        ClinicSettingsUpdateRequest request, CancellationToken ct)
        => Ok(await settings.UpdateClinicAsync(request, ct));

    // ---- Klinik çalışma saatleri (hekim saatleri /api/v1/working-hours ucundadır) ----

    [HttpGet("working-hours")]
    [HasPermission("settings.view")]
    public async Task<ActionResult<IReadOnlyList<ClinicWorkingHourDto>>> GetWorkingHours(
        [FromQuery] long? clinicId, CancellationToken ct)
        => Ok(await settings.GetClinicWorkingHoursAsync(clinicId, ct));

    [HttpPut("working-hours")]
    [HasPermission("settings.update")]
    public async Task<ActionResult<IReadOnlyList<ClinicWorkingHourDto>>> SaveWorkingHours(
        ClinicWorkingHoursSaveRequest request, CancellationToken ct)
        => Ok(await settings.SaveClinicWorkingHoursAsync(request, ct));

    // ---- Personel ----

    [HttpGet("staff")]
    [HasPermission("settings.staff")]
    public async Task<ActionResult<IReadOnlyList<StaffDto>>> ListStaff(
        [FromQuery] bool includeInactive = true, CancellationToken ct = default)
        => Ok(await settings.ListStaffAsync(includeInactive, ct));

    [HttpGet("staff/{id:long}")]
    [HasPermission("settings.staff")]
    public async Task<ActionResult<StaffDto>> GetStaff(long id, CancellationToken ct)
        => Ok(await settings.GetStaffAsync(id, ct));

    /// <summary>Davet: kullanıcı oluşturulur, geçici şifre YALNIZ bu yanıtta döner (MustChangePassword).</summary>
    [HttpPost("staff")]
    [HasPermission("settings.staff")]
    public async Task<ActionResult<StaffInviteResultDto>> InviteStaff(
        StaffInviteRequest request, CancellationToken ct)
    {
        var result = await settings.InviteStaffAsync(request, ct);
        return CreatedAtAction(nameof(GetStaff), new { id = result.User.Id }, result);
    }

    [HttpPut("staff/{id:long}")]
    [HasPermission("settings.staff")]
    public async Task<ActionResult<StaffDto>> UpdateStaff(long id, StaffUpdateRequest request, CancellationToken ct)
        => Ok(await settings.UpdateStaffAsync(id, request, ct));

    [HttpPost("staff/{id:long}/reset-password")]
    [HasPermission("settings.staff")]
    public async Task<ActionResult<TemporaryPasswordDto>> ResetStaffPassword(long id, CancellationToken ct)
        => Ok(await settings.ResetStaffPasswordAsync(id, ct));

    /// <summary>Pasife alır. Kendini ve son aktif Owner'ı pasife almak reddedilir.</summary>
    [HttpDelete("staff/{id:long}")]
    [HasPermission("settings.staff")]
    public async Task<IActionResult> DeactivateStaff(long id, CancellationToken ct)
    {
        await settings.DeactivateStaffAsync(id, ct);
        return NoContent();
    }

    // ---- Yetki matrisi ----

    [HttpGet("roles")]
    [HasPermission("settings.staff")]
    public async Task<ActionResult<IReadOnlyList<RolePermissionsDto>>> ListRoles(CancellationToken ct)
        => Ok(await settings.ListRolesAsync(ct));

    [HttpGet("permissions")]
    [HasPermission("settings.staff")]
    public ActionResult<PermissionCatalogDto> PermissionCatalog()
        => Ok(settings.GetPermissionCatalog());

    /// <summary>Sistem rolleri düzenlenebilir; Owner rolünden settings.staff kaldırılamaz.</summary>
    [HttpPut("roles/{id:long}/permissions")]
    [HasPermission("settings.staff")]
    public async Task<ActionResult<RolePermissionsDto>> UpdateRolePermissions(
        long id, RolePermissionsUpdateRequest request, CancellationToken ct)
        => Ok(await settings.UpdateRolePermissionsAsync(id, request, ct));

    // ---- Entegrasyonlar ----

    /// <summary>Sırlar maskeli döner (••••1234); düz metin hiçbir zaman geri okunmaz.</summary>
    [HttpGet("integrations")]
    [HasPermission("settings.integrations")]
    public async Task<ActionResult<IReadOnlyList<IntegrationSettingDto>>> ListIntegrations(CancellationToken ct)
        => Ok(await settings.ListIntegrationsAsync(ct));

    [HttpPut("integrations/{key}")]
    [HasPermission("settings.integrations")]
    public async Task<ActionResult<IntegrationSettingDto>> UpdateIntegration(
        string key, IntegrationSettingUpdateRequest request, CancellationToken ct)
        => Ok(await settings.UpdateIntegrationAsync(key, request, ct));

    [HttpPost("integrations/{key}/test")]
    [HasPermission("settings.integrations")]
    public async Task<ActionResult<IntegrationTestResultDto>> TestIntegration(string key, CancellationToken ct)
        => Ok(await settings.TestIntegrationAsync(key, ct));
}
