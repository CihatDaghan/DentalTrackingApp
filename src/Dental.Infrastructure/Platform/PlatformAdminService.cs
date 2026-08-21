using Dental.Application.Abstractions;
using Dental.Application.Auth;
using Dental.Application.Common;
using Dental.Application.Platform;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Enabiz;
using Dental.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Platform;

/// <summary>
/// Süper admin (platform) yönetimi: plan, duyuru, kiracı, entegrasyon sağlığı ve
/// audit'li kimliğe bürünme.
///
/// <para>Sorgular kiracı süzgecini <c>IgnoreQueryFilters</c> ile bilerek atlar — süper admin
/// bağlamı zaten filtreyi devre dışı bırakır, ancak silinmiş kiracıları da görebilmek için
/// soft-delete süzgeci de kaldırılır.</para>
/// </summary>
public sealed class PlatformAdminService(
    AppDbContext db,
    ITenantContext tenant,
    ITokenService tokens,
    IClock clock,
    EnabizModeResolver enabizMode) : IPlatformAdminService
{
    /// <summary>Kimliğe bürünme token'ının ömrü — kasıtlı olarak kısa; refresh üretilmez.</summary>
    public static readonly TimeSpan ImpersonationLifetime = TimeSpan.FromMinutes(15);

    private static readonly string[] IntegrationKeys = ["EInvoice", "Sms", "WhatsApp", "Payment", "Enabiz"];

    // ---- Plan ----

    public async Task<IReadOnlyList<PlanDto>> ListPlansAsync(bool includeInactive, CancellationToken ct = default)
    {
        var source = db.Plans.AsNoTracking().AsQueryable();
        if (!includeInactive) source = source.Where(p => p.IsActive);
        return await ProjectPlans(source.OrderBy(p => p.SortOrder).ThenBy(p => p.PriceMonthly)).ToListAsync(ct);
    }

    public async Task<PlanDto> GetPlanAsync(long id, CancellationToken ct = default) =>
        await ProjectPlans(db.Plans.AsNoTracking().Where(p => p.Id == id)).FirstOrDefaultAsync(ct)
        ?? throw new KeyNotFoundException("Plan bulunamadı.");

    public async Task<PlanDto> CreatePlanAsync(PlanUpsertRequest request, CancellationToken ct = default)
    {
        var code = NormalizeCode(request.Code);
        if (await db.Plans.AnyAsync(p => p.Code == code, ct))
            throw new InvalidOperationException($"'{code}' kodlu plan zaten var.");

        var plan = new Plan { Code = code, Name = request.Name.Trim() };
        Apply(plan, request);
        db.Plans.Add(plan);
        await db.SaveChangesAsync(ct);
        return await GetPlanAsync(plan.Id, ct);
    }

    public async Task<PlanDto> UpdatePlanAsync(long id, PlanUpsertRequest request, CancellationToken ct = default)
    {
        var plan = await db.Plans.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new KeyNotFoundException("Plan bulunamadı.");
        var code = NormalizeCode(request.Code);
        if (await db.Plans.AnyAsync(p => p.Code == code && p.Id != id, ct))
            throw new InvalidOperationException($"'{code}' kodlu plan zaten var.");

        // Kod değişirse kiracıların PlanCode'u da taşınır (yetim referans kalmasın).
        if (!string.Equals(plan.Code, code, StringComparison.Ordinal))
        {
            var oldCode = plan.Code;
            await db.Tenants.IgnoreQueryFilters().Where(t => t.PlanCode == oldCode)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.PlanCode, code), ct);
        }

        plan.Code = code;
        plan.Name = request.Name.Trim();
        Apply(plan, request);
        await db.SaveChangesAsync(ct);
        return await GetPlanAsync(id, ct);
    }

    public async Task DeletePlanAsync(long id, CancellationToken ct = default)
    {
        var plan = await db.Plans.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new KeyNotFoundException("Plan bulunamadı.");
        if (await db.Tenants.IgnoreQueryFilters().AnyAsync(t => t.PlanCode == plan.Code, ct))
            throw new InvalidOperationException("Kiracısı olan plan silinemez; önce pasife alın.");

        db.Plans.Remove(plan); // Plan ISoftDelete değildir: gerçekten silinir.
        await db.SaveChangesAsync(ct);
    }

    private static void Apply(Plan plan, PlanUpsertRequest request)
    {
        plan.MaxUsers = request.MaxUsers;
        plan.MaxPatients = request.MaxPatients;
        plan.MonthlySmsQuota = request.MonthlySmsQuota;
        plan.StorageGb = request.StorageGb;
        plan.PriceMonthly = request.PriceMonthly;
        plan.IsActive = request.IsActive;
        plan.SortOrder = request.SortOrder;
    }

    private IQueryable<PlanDto> ProjectPlans(IQueryable<Plan> source) =>
        source.Select(p => new PlanDto(
            p.Id, p.Code, p.Name, p.MaxUsers, p.MaxPatients, p.MonthlySmsQuota, p.StorageGb,
            p.PriceMonthly, p.IsActive, p.SortOrder,
            db.Tenants.IgnoreQueryFilters().Count(t => t.PlanCode == p.Code)));

    // ---- Duyuru ----

    public async Task<IReadOnlyList<AnnouncementDto>> ListAnnouncementsAsync(CancellationToken ct = default) =>
        await ProjectAnnouncements(db.Announcements.AsNoTracking().OrderByDescending(a => a.StartsAtUtc))
            .ToListAsync(ct);

    public async Task<AnnouncementDto> GetAnnouncementAsync(long id, CancellationToken ct = default) =>
        await ProjectAnnouncements(db.Announcements.AsNoTracking().Where(a => a.Id == id)).FirstOrDefaultAsync(ct)
        ?? throw new KeyNotFoundException("Duyuru bulunamadı.");

    public async Task<AnnouncementDto> CreateAnnouncementAsync(
        AnnouncementUpsertRequest request, CancellationToken ct = default)
    {
        await EnsureTargetTenantAsync(request.TargetTenantId, ct);
        var announcement = new Announcement
        {
            Title = request.Title.Trim(),
            Body = request.Body.Trim(),
            Severity = request.Severity,
            StartsAtUtc = request.StartsAtUtc ?? clock.UtcNow,
            EndsAtUtc = request.EndsAtUtc,
            IsActive = request.IsActive,
            TargetTenantId = request.TargetTenantId,
        };
        EnsureWindow(announcement);
        db.Announcements.Add(announcement);
        await db.SaveChangesAsync(ct);
        return await GetAnnouncementAsync(announcement.Id, ct);
    }

    public async Task<AnnouncementDto> UpdateAnnouncementAsync(
        long id, AnnouncementUpsertRequest request, CancellationToken ct = default)
    {
        var announcement = await db.Announcements.FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new KeyNotFoundException("Duyuru bulunamadı.");
        await EnsureTargetTenantAsync(request.TargetTenantId, ct);

        announcement.Title = request.Title.Trim();
        announcement.Body = request.Body.Trim();
        announcement.Severity = request.Severity;
        announcement.StartsAtUtc = request.StartsAtUtc ?? announcement.StartsAtUtc;
        announcement.EndsAtUtc = request.EndsAtUtc;
        announcement.IsActive = request.IsActive;
        announcement.TargetTenantId = request.TargetTenantId;
        EnsureWindow(announcement);

        await db.SaveChangesAsync(ct);
        return await GetAnnouncementAsync(id, ct);
    }

    public async Task DeleteAnnouncementAsync(long id, CancellationToken ct = default)
    {
        var announcement = await db.Announcements.FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw new KeyNotFoundException("Duyuru bulunamadı.");
        db.Announcements.Remove(announcement);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ActiveAnnouncementDto>> GetActiveAnnouncementsAsync(
        long? tenantId, CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        return await db.Announcements.AsNoTracking()
            .Where(a => a.IsActive && a.StartsAtUtc <= now && (a.EndsAtUtc == null || a.EndsAtUtc > now))
            .Where(a => a.TargetTenantId == null || a.TargetTenantId == tenantId)
            .OrderByDescending(a => a.Severity).ThenByDescending(a => a.StartsAtUtc)
            .Select(a => new ActiveAnnouncementDto(a.Id, a.Title, a.Body, a.Severity, a.StartsAtUtc, a.EndsAtUtc))
            .ToListAsync(ct);
    }

    private static void EnsureWindow(Announcement announcement)
    {
        if (announcement.EndsAtUtc is { } end && end <= announcement.StartsAtUtc)
            throw new InvalidOperationException("Duyuru bitiş tarihi başlangıçtan sonra olmalıdır.");
    }

    private async Task EnsureTargetTenantAsync(long? tenantId, CancellationToken ct)
    {
        if (tenantId is { } id && !await db.Tenants.IgnoreQueryFilters().AnyAsync(t => t.Id == id, ct))
            throw new KeyNotFoundException("Hedef kiracı bulunamadı.");
    }

    private IQueryable<AnnouncementDto> ProjectAnnouncements(IQueryable<Announcement> source) =>
        source.Select(a => new AnnouncementDto(
            a.Id, a.Title, a.Body, a.Severity, a.StartsAtUtc, a.EndsAtUtc, a.IsActive, a.TargetTenantId,
            db.Tenants.IgnoreQueryFilters().Where(t => t.Id == a.TargetTenantId).Select(t => t.Name).FirstOrDefault(),
            a.CreatedAtUtc));

    // ---- Kiracı ----

    public async Task<PagedResult<TenantListItemDto>> ListTenantsAsync(
        TenantListQuery query, CancellationToken ct = default)
    {
        var source = db.Tenants.IgnoreQueryFilters().AsNoTracking().AsQueryable();
        if (!query.IncludeDeleted) source = source.Where(t => !t.IsDeleted);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            source = source.Where(t => t.Name.Contains(search) || (t.TaxNumber != null && t.TaxNumber.Contains(search)));
        }
        if (query.Status is { } status) source = source.Where(t => t.Status == status);
        if (!string.IsNullOrWhiteSpace(query.PlanCode)) source = source.Where(t => t.PlanCode == query.PlanCode);

        var page = new PageRequest(query.Page, query.PageSize);
        var totalCount = await source.CountAsync(ct);
        var items = await source
            .OrderByDescending(t => t.CreatedAtUtc).ThenBy(t => t.Id)
            .Skip(page.Skip).Take(page.EffectivePageSize)
            .Select(t => new TenantListItemDto(
                t.Id, t.Name, t.LegalType, t.Status, t.PlanCode,
                db.Plans.Where(p => p.Code == t.PlanCode).Select(p => p.Name).FirstOrDefault(),
                t.CreatedAtUtc, t.TrialEndsAtUtc, t.IsDeleted,
                new TenantUsageDto(
                    db.Users.IgnoreQueryFilters().Count(u => u.TenantId == t.Id),
                    db.Patients.IgnoreQueryFilters().Count(p => p.TenantId == t.Id && !p.IsDeleted),
                    db.Appointments.IgnoreQueryFilters().Count(a => a.TenantId == t.Id && !a.IsDeleted),
                    db.Invoices.IgnoreQueryFilters().Count(i => i.TenantId == t.Id && !i.IsDeleted),
                    db.TreatmentRecords.IgnoreQueryFilters().Count(r => r.TenantId == t.Id && !r.IsDeleted),
                    db.AuditLogs.Where(a => a.TenantId == t.Id).Max(a => (DateTime?)a.AtUtc))))
            .ToListAsync(ct);

        return new PagedResult<TenantListItemDto>(items, page.Page, page.EffectivePageSize, totalCount);
    }

    public async Task<TenantDetailDto> GetTenantAsync(long id, CancellationToken ct = default)
    {
        var tenantRow = await db.Tenants.IgnoreQueryFilters().AsNoTracking()
            .Where(t => t.Id == id)
            .Select(t => new
            {
                t.Id, t.Name, t.LegalType, t.TaxNumber, t.TaxOffice, t.HasHealthTourismAuthorization,
                t.Status, t.PlanCode, t.CreatedAtUtc, t.TrialEndsAtUtc, t.IsDeleted,
                PlanName = db.Plans.Where(p => p.Code == t.PlanCode).Select(p => p.Name).FirstOrDefault(),
                Usage = new TenantUsageDto(
                    db.Users.IgnoreQueryFilters().Count(u => u.TenantId == t.Id),
                    db.Patients.IgnoreQueryFilters().Count(p => p.TenantId == t.Id && !p.IsDeleted),
                    db.Appointments.IgnoreQueryFilters().Count(a => a.TenantId == t.Id && !a.IsDeleted),
                    db.Invoices.IgnoreQueryFilters().Count(i => i.TenantId == t.Id && !i.IsDeleted),
                    db.TreatmentRecords.IgnoreQueryFilters().Count(r => r.TenantId == t.Id && !r.IsDeleted),
                    db.AuditLogs.Where(a => a.TenantId == t.Id).Max(a => (DateTime?)a.AtUtc)),
            })
            .FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("Kiracı bulunamadı.");

        var clinics = await db.Clinics.IgnoreQueryFilters().AsNoTracking()
            .Where(c => c.TenantId == id && !c.IsDeleted)
            .OrderBy(c => c.Id)
            .Select(c => new TenantClinicDto(c.Id, c.Name, c.City, c.Phone, c.CkysCode))
            .ToListAsync(ct);

        var owners = await db.Users.IgnoreQueryFilters().AsNoTracking()
            .Where(u => u.TenantId == id && u.UserType == UserType.Owner)
            .OrderBy(u => u.Id)
            .Select(u => new TenantOwnerDto(u.Id, u.Email ?? "", u.FirstName + " " + u.LastName, u.IsActive))
            .ToListAsync(ct);

        return new TenantDetailDto(
            tenantRow.Id, tenantRow.Name, tenantRow.LegalType, tenantRow.TaxNumber, tenantRow.TaxOffice,
            tenantRow.HasHealthTourismAuthorization, tenantRow.Status, tenantRow.PlanCode, tenantRow.PlanName,
            tenantRow.CreatedAtUtc, tenantRow.TrialEndsAtUtc, tenantRow.IsDeleted,
            tenantRow.Usage, clinics, owners);
    }

    public async Task<TenantDetailDto> UpdateTenantAsync(
        long id, TenantUpdateRequest request, CancellationToken ct = default)
    {
        var target = await db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new KeyNotFoundException("Kiracı bulunamadı.");

        if (!string.IsNullOrWhiteSpace(request.Name)) target.Name = request.Name.Trim();
        if (request.PlanCode is { } planCode)
        {
            var code = NormalizeCode(planCode);
            if (code.Length > 0 && !await db.Plans.AnyAsync(p => p.Code == code && p.IsActive, ct))
                throw new KeyNotFoundException($"'{code}' kodlu aktif plan bulunamadı.");
            target.PlanCode = code.Length == 0 ? null : code;
        }
        if (request.Status is { } status) target.Status = status;
        if (request.TrialEndsAtUtc is { } trialEnds) target.TrialEndsAtUtc = trialEnds;

        await db.SaveChangesAsync(ct);
        return await GetTenantAsync(id, ct);
    }

    public async Task DeleteTenantAsync(long id, bool confirm, CancellationToken ct = default)
    {
        if (!confirm)
            throw new InvalidOperationException("Kiracı silme işlemi onay gerektirir (confirm=true).");

        var target = await db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw new KeyNotFoundException("Kiracı bulunamadı.");
        if (target.IsDeleted) return;

        // Yumuşak silme: kiracı askıya alınır ve silinmiş işaretlenir; veri saklama
        // yükümlülüğü nedeniyle alt kayıtlar bilinçli olarak SİLİNMEZ (yalnız erişim kapanır).
        target.Status = TenantStatus.Suspended;
        target.IsDeleted = true;
        target.DeletedAtUtc = clock.UtcNow;
        target.DeletedByUserId = tenant.UserId;
        await db.Users.IgnoreQueryFilters().Where(u => u.TenantId == id && u.IsActive)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.IsActive, false), ct);
        await db.SaveChangesAsync(ct);
    }

    // ---- Kimliğe bürünme (audit'li) ----

    public async Task<ImpersonationResponse> ImpersonateAsync(
        long tenantId, string? ip, string? userAgent, CancellationToken ct = default)
    {
        if (!tenant.IsSuperAdmin)
            throw new UnauthorizedAccessException("Kimliğe bürünme yalnız süper admin yetkisiyle yapılabilir.");

        var target = await db.Tenants.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId, ct)
            ?? throw new KeyNotFoundException("Kiracı bulunamadı.");
        if (target.IsDeleted)
            throw new InvalidOperationException("Silinmiş kiracıya bürünülemez.");

        var owner = await db.Users.IgnoreQueryFilters()
            .Where(u => u.TenantId == tenantId && u.UserType == UserType.Owner && u.IsActive)
            .OrderBy(u => u.Id)
            .FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("Kiracının aktif Owner kullanıcısı yok.");

        var clinicId = await db.UserClinics.IgnoreQueryFilters()
            .Where(uc => uc.UserId == owner.Id)
            .OrderByDescending(uc => uc.IsDefault)
            .Select(uc => (long?)uc.ClinicId)
            .FirstOrDefaultAsync(ct);

        var permissions = await db.UserRoles.IgnoreQueryFilters()
            .Where(ur => ur.UserId == owner.Id)
            .SelectMany(ur => ur.Role!.Permissions)
            .Select(rp => rp.Permission!.Code)
            .Distinct()
            .OrderBy(c => c)
            .ToListAsync(ct);

        var impersonatorId = tenant.UserId
            ?? throw new InvalidOperationException("Kimliğe bürünen kullanıcı bağlamı yok.");

        var token = tokens.CreateImpersonationToken(
            owner, clinicId, permissions, impersonatorId, ImpersonationLifetime);

        // AuditLog: kim, hangi kiracı, ne zaman. Refresh token ÜRETİLMEZ.
        var now = clock.UtcNow;
        var audit = new AuditLog
        {
            TenantId = tenantId,
            UserId = impersonatorId,
            ActionType = AuditActionType.Impersonation,
            EntityName = nameof(Tenant),
            EntityId = tenantId,
            NewValuesJson = System.Text.Json.JsonSerializer.Serialize(new
            {
                impersonatedUserId = owner.Id,
                impersonatedEmail = owner.Email,
                tenantName = target.Name,
                expiresAtUtc = now.Add(ImpersonationLifetime),
            }),
            Ip = ip,
            UserAgent = userAgent,
            AtUtc = now,
        };
        db.AuditLogs.Add(audit);
        await db.SaveChangesAsync(ct);

        return new ImpersonationResponse(
            token, (int)ImpersonationLifetime.TotalSeconds, now.Add(ImpersonationLifetime),
            tenantId, target.Name, owner.Id, owner.Email ?? "", audit.Id);
    }

    // ---- Entegrasyon sağlığı ----

    public async Task<IReadOnlyList<TenantIntegrationHealthDto>> GetIntegrationHealthAsync(
        long? tenantId, CancellationToken ct = default)
    {
        var since = clock.UtcNow.AddHours(-24);

        var tenants = await db.Tenants.IgnoreQueryFilters().AsNoTracking()
            .Where(t => !t.IsDeleted && (tenantId == null || t.Id == tenantId))
            .OrderBy(t => t.Id)
            .Select(t => new { t.Id, t.Name, t.Status })
            .ToListAsync(ct);
        var tenantIds = tenants.Select(t => t.Id).ToList();

        var settings = await db.TenantIntegrationSettings.IgnoreQueryFilters().AsNoTracking()
            .Where(s => tenantIds.Contains(s.TenantId) && !s.IsDeleted)
            .Select(s => new
            {
                s.TenantId, s.IntegrationKey, s.ProviderKey, s.Environment, s.IsEnabled,
                HasCredentials = s.SettingsJsonEncrypted != null,
            })
            .ToListAsync(ct);

        // Tek sorguda kiracı + entegrasyon bazlı çağrı özeti (son 24 sa).
        var stats = await db.IntegrationCallLogs.AsNoTracking()
            .Where(l => l.TenantId != null && tenantIds.Contains(l.TenantId.Value) && l.CreatedAtUtc >= since)
            .GroupBy(l => new { TenantId = l.TenantId!.Value, l.IntegrationKey })
            .Select(g => new
            {
                g.Key.TenantId,
                g.Key.IntegrationKey,
                CallCount = g.Count(),
                FailureCount = g.Count(l => !l.IsSuccess),
                LastSuccessUtc = g.Where(l => l.IsSuccess).Max(l => (DateTime?)l.CreatedAtUtc),
                LastFailureUtc = g.Where(l => !l.IsSuccess).Max(l => (DateTime?)l.CreatedAtUtc),
            })
            .ToListAsync(ct);

        // Son hata metinleri ayrı ve sınırlı bir sorguyla çekilir (gruplamada string alınamıyor).
        var lastErrors = await db.IntegrationCallLogs.AsNoTracking()
            .Where(l => l.TenantId != null && tenantIds.Contains(l.TenantId.Value)
                        && l.CreatedAtUtc >= since && !l.IsSuccess)
            .OrderByDescending(l => l.CreatedAtUtc)
            .Select(l => new { TenantId = l.TenantId!.Value, l.IntegrationKey, l.ResponseSummary, l.CreatedAtUtc })
            .Take(500)
            .ToListAsync(ct);

        var result = new List<TenantIntegrationHealthDto>(tenants.Count);
        foreach (var t in tenants)
        {
            var mode = await enabizMode.ResolveAsync(t.Id, ct);
            var rows = IntegrationKeys.Select(key =>
            {
                var setting = settings.FirstOrDefault(s => s.TenantId == t.Id && s.IntegrationKey == key);
                var stat = stats.FirstOrDefault(s => s.TenantId == t.Id && s.IntegrationKey == key);
                var error = lastErrors.FirstOrDefault(e => e.TenantId == t.Id && e.IntegrationKey == key);
                return new IntegrationHealthRowDto(
                    key,
                    setting?.ProviderKey,
                    setting?.Environment ?? "Test",
                    setting?.IsEnabled ?? false,
                    setting?.HasCredentials ?? false,
                    stat?.LastSuccessUtc,
                    stat?.LastFailureUtc,
                    stat?.CallCount ?? 0,
                    stat?.FailureCount ?? 0,
                    error?.ResponseSummary);
            }).ToList();

            result.Add(new TenantIntegrationHealthDto(
                t.Id, t.Name, t.Status, rows, mode.Mode, mode.RequestedMode, mode.KtsRegistered));
        }
        return result;
    }

    private static string NormalizeCode(string code) => code.Trim().ToLowerInvariant();
}
