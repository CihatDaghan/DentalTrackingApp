using Dental.Api.Auth;
using Dental.Application.Common;
using Dental.Application.Platform;
using Dental.Application.Tenants;
using Dental.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dental.Api.Controllers;

/// <summary>Süper-admin kiracı yönetimi: liste/detay/güncelleme/soft delete + audit'li impersonation.</summary>
[ApiController]
[Route("api/v1/admin/tenants")]
[Authorize(Policy = AuthPolicies.SuperAdmin)]
public sealed class AdminTenantsController(
    ITenantProvisioningService provisioning,
    IPlatformAdminService platform) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<TenantListItemDto>>> List(
        [FromQuery] string? search, [FromQuery] TenantStatus? status, [FromQuery] string? planCode,
        [FromQuery] bool includeDeleted = false,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
        => Ok(await platform.ListTenantsAsync(
            new TenantListQuery(search, status, planCode, includeDeleted, page, pageSize), ct));

    [HttpGet("{id:long}")]
    public async Task<ActionResult<TenantDetailDto>> Get(long id, CancellationToken ct)
        => Ok(await platform.GetTenantAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<CreateTenantResult>> Create(CreateTenantRequest request, CancellationToken ct)
        => Ok(await provisioning.CreateAsync(request, ct));

    /// <summary>Plan değişikliği, durum (Active/Suspended/Trial) ve deneme bitiş tarihi.</summary>
    [HttpPut("{id:long}")]
    public async Task<ActionResult<TenantDetailDto>> Update(long id, TenantUpdateRequest request, CancellationToken ct)
        => Ok(await platform.UpdateTenantAsync(id, request, ct));

    /// <summary>Soft delete: kiracı askıya alınır, kullanıcıları pasife çekilir; veri saklanır.</summary>
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, [FromQuery] bool confirm = false, CancellationToken ct = default)
    {
        await platform.DeleteTenantAsync(id, confirm, ct);
        return NoContent();
    }

    /// <summary>
    /// Hedef kiracının Owner'ı adına 15 dk ömürlü access token üretir.
    /// <c>impersonated_by</c> claim'i eklenir, AuditLog (Impersonation) yazılır ve
    /// REFRESH TOKEN ÜRETİLMEZ — oturum uzatılamaz.
    /// </summary>
    [HttpPost("{id:long}/impersonate")]
    public async Task<ActionResult<ImpersonationResponse>> Impersonate(long id, CancellationToken ct)
        => Ok(await platform.ImpersonateAsync(
            id, HttpContext.Connection.RemoteIpAddress?.ToString(), Request.Headers.UserAgent, ct));
}

/// <summary>Abonelik planları (global).</summary>
[ApiController]
[Route("api/v1/admin/plans")]
[Authorize(Policy = AuthPolicies.SuperAdmin)]
public sealed class AdminPlansController(IPlatformAdminService platform) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<PlanDto>>> List(
        [FromQuery] bool includeInactive = true, CancellationToken ct = default)
        => Ok(await platform.ListPlansAsync(includeInactive, ct));

    [HttpGet("{id:long}")]
    public async Task<ActionResult<PlanDto>> Get(long id, CancellationToken ct)
        => Ok(await platform.GetPlanAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<PlanDto>> Create(PlanUpsertRequest request, CancellationToken ct)
    {
        var dto = await platform.CreatePlanAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<PlanDto>> Update(long id, PlanUpsertRequest request, CancellationToken ct)
        => Ok(await platform.UpdatePlanAsync(id, request, ct));

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await platform.DeletePlanAsync(id, ct);
        return NoContent();
    }
}

/// <summary>Platform duyuruları (global; TargetTenantId ile tek kiracıya yönlendirilebilir).</summary>
[ApiController]
[Route("api/v1/admin/announcements")]
[Authorize(Policy = AuthPolicies.SuperAdmin)]
public sealed class AdminAnnouncementsController(IPlatformAdminService platform) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AnnouncementDto>>> List(CancellationToken ct)
        => Ok(await platform.ListAnnouncementsAsync(ct));

    [HttpGet("{id:long}")]
    public async Task<ActionResult<AnnouncementDto>> Get(long id, CancellationToken ct)
        => Ok(await platform.GetAnnouncementAsync(id, ct));

    [HttpPost]
    public async Task<ActionResult<AnnouncementDto>> Create(AnnouncementUpsertRequest request, CancellationToken ct)
    {
        var dto = await platform.CreateAnnouncementAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = dto.Id }, dto);
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<AnnouncementDto>> Update(
        long id, AnnouncementUpsertRequest request, CancellationToken ct)
        => Ok(await platform.UpdateAnnouncementAsync(id, request, ct));

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken ct)
    {
        await platform.DeleteAnnouncementAsync(id, ct);
        return NoContent();
    }
}

/// <summary>Kiracı bazında entegrasyon sağlığı (son 24 saatlik çağrı özeti + e-Nabız modu).</summary>
[ApiController]
[Route("api/v1/admin/integration-health")]
[Authorize(Policy = AuthPolicies.SuperAdmin)]
public sealed class AdminIntegrationHealthController(IPlatformAdminService platform) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TenantIntegrationHealthDto>>> Get(
        [FromQuery] long? tenantId, CancellationToken ct)
        => Ok(await platform.GetIntegrationHealthAsync(tenantId, ct));
}

/// <summary>Uygulama içi duyuru banner'ı — kimliği doğrulanmış her kullanıcıya açıktır.</summary>
[ApiController]
[Route("api/v1/announcements")]
[Authorize]
public sealed class AnnouncementsController(
    IPlatformAdminService platform,
    Dental.Application.Abstractions.ITenantContext tenant) : ControllerBase
{
    [HttpGet("active")]
    public async Task<ActionResult<IReadOnlyList<ActiveAnnouncementDto>>> Active(CancellationToken ct)
        => Ok(await platform.GetActiveAnnouncementsAsync(tenant.TenantId, ct));
}
