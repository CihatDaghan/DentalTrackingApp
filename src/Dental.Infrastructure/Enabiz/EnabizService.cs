using System.Text.Json;
using Dental.Application.Abstractions;
using Dental.Application.Common;
using Dental.Application.Enabiz;
using Dental.Domain.Common;
using Dental.Domain.Enums;
using Dental.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Dental.Infrastructure.Enabiz;

/// <summary>e-Nabız kuyruğu sorguları + kiracı ayar yönetimi (API tarafı).</summary>
public sealed class EnabizService(
    AppDbContext db,
    ITenantContext tenant,
    IIntegrationSettingsStore store,
    EnabizModeResolver modes,
    IConfiguration configuration) : IEnabizService
{
    /// <summary>
    /// Ayar kaydı ilk kez oluşturulurken kullanılacak sürücü anahtarı.
    /// Sabit "sys" YAZILMAZ: geliştirme/test ortamları <c>Integrations:DefaultEnabizProvider</c> ile
    /// sahte sürücüde kalabilmelidir — aksi hâlde ayar ekranına dokunmak, kimlik bilgisi olmayan bir
    /// kurulumu Bakanlık sunucusuna istek atmaya başlatır.
    /// </summary>
    private string DefaultProviderKey =>
        configuration["Integrations:DefaultEnabizProvider"] is { Length: > 0 } key ? key : "sys";

    public async Task<PagedResult<EnabizSubmissionListItemDto>> ListAsync(
        EnabizSubmissionState? state = null,
        EnabizPacketType? packetType = null,
        DateOnly? from = null,
        DateOnly? to = null,
        int page = 1,
        int pageSize = 25,
        CancellationToken ct = default)
    {
        var request = new PageRequest(page, pageSize);
        var query = db.EnabizSubmissions.AsNoTracking();

        if (state is { } s) query = query.Where(x => x.State == s);
        if (packetType is { } p) query = query.Where(x => x.PacketType == p);
        if (from is { } f) query = query.Where(x => x.CreatedAtUtc >= TrTime.DayRangeUtc(f).StartUtc);
        if (to is { } t) query = query.Where(x => x.CreatedAtUtc < TrTime.DayRangeUtc(t).EndUtc);

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(x => x.Id)
            .Skip(request.Skip)
            .Take(request.EffectivePageSize)
            .Select(x => new
            {
                Submission = x,
                Visit = db.Visits.Where(v => v.Id == x.VisitId)
                    .Select(v => new { v.ProtocolNo, v.PatientId })
                    .FirstOrDefault(),
            })
            .ToListAsync(ct);

        var patientIds = items.Where(i => i.Visit != null).Select(i => i.Visit!.PatientId).Distinct().ToList();
        var patients = await db.Patients.AsNoTracking()
            .Where(p => patientIds.Contains(p.Id))
            .Select(p => new { p.Id, p.FirstName, p.LastName })
            .ToDictionaryAsync(p => p.Id, p => $"{p.FirstName} {p.LastName}", ct);

        var dtos = items.Select(i => new EnabizSubmissionListItemDto(
            i.Submission.Id,
            i.Submission.PacketType,
            i.Submission.State,
            i.Submission.VisitId,
            i.Visit?.ProtocolNo,
            i.Visit?.PatientId,
            i.Visit is not null && patients.TryGetValue(i.Visit.PatientId, out var name) ? name : null,
            i.Submission.SysTakipNo,
            i.Submission.AttemptCount,
            i.Submission.NextAttemptAtUtc,
            i.Submission.SentAtUtc,
            i.Submission.LastErrorCode,
            i.Submission.LastErrorMessage,
            i.Submission.CreatedAtUtc)).ToList();

        return new PagedResult<EnabizSubmissionListItemDto>(
            dtos, request.Page, request.EffectivePageSize, total);
    }

    public async Task<EnabizSubmissionDto> GetAsync(long id, CancellationToken ct = default)
    {
        var submission = await db.EnabizSubmissions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new KeyNotFoundException("e-Nabız gönderim kaydı bulunamadı.");

        var visit = submission.VisitId is { } visitId
            ? await db.Visits.AsNoTracking().Where(v => v.Id == visitId)
                .Select(v => new { v.ProtocolNo, v.PatientId }).FirstOrDefaultAsync(ct)
            : null;

        var patientName = visit is null ? null : await db.Patients.AsNoTracking()
            .Where(p => p.Id == visit.PatientId)
            .Select(p => p.FirstName + " " + p.LastName)
            .FirstOrDefaultAsync(ct);

        return new EnabizSubmissionDto(
            submission.Id,
            submission.PacketType,
            submission.State,
            submission.VisitId,
            visit?.ProtocolNo,
            visit?.PatientId,
            patientName,
            submission.TreatmentRecordId,
            submission.PrescriptionId,
            submission.FacilityCode,
            submission.SysTakipNo,
            submission.DependsOnSubmissionId,
            submission.AttemptCount,
            submission.NextAttemptAtUtc,
            submission.SentAtUtc,
            submission.LastErrorCode,
            submission.LastErrorMessage,
            submission.PhysicianSignState,
            submission.RegenerateOnSend,
            submission.PayloadXml,
            submission.CreatedAtUtc);
    }

    public async Task<EnabizStatusDto> GetStatusAsync(CancellationToken ct = default)
    {
        var tenantId = RequireTenantId();
        var mode = await modes.ResolveAsync(tenantId, ct);

        var counts = await db.EnabizSubmissions.AsNoTracking()
            .GroupBy(s => s.State)
            .Select(g => new { State = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        int Count(EnabizSubmissionState state) =>
            counts.FirstOrDefault(c => c.State == state)?.Count ?? 0;

        var lastSent = await db.EnabizSubmissions.AsNoTracking()
            .Where(s => s.SentAtUtc != null)
            .MaxAsync(s => (DateTime?)s.SentAtUtc, ct);

        var lastSync = await db.SkrsCodeSystems.AsNoTracking()
            .MaxAsync(s => (DateTime?)s.LastSyncAtUtc, ct);

        return new EnabizStatusDto(
            mode.Mode,
            mode.KtsRegistered,
            CanGoLive: mode.KtsRegistered,
            mode.CkysCode,
            mode.HasPassword && !string.IsNullOrWhiteSpace(mode.UssUsername),
            Count(EnabizSubmissionState.Held),
            Count(EnabizSubmissionState.Queued),
            Count(EnabizSubmissionState.Sending),
            Count(EnabizSubmissionState.Accepted),
            Count(EnabizSubmissionState.Rejected),
            Count(EnabizSubmissionState.ManualReview),
            lastSync,
            lastSent);
    }

    public async Task<EnabizSettingsDto> GetSettingsAsync(CancellationToken ct = default)
    {
        var tenantId = RequireTenantId();
        var mode = await modes.ResolveAsync(tenantId, ct);
        return Map(mode);
    }

    public async Task<EnabizSettingsDto> UpdateSettingsAsync(
        EnabizSettingsRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tenantId = RequireTenantId();

        // Canlıya geçiş iki anahtarlıdır: KTS tescili olmadan Live seçilemez.
        if (request.Mode == EnabizMode.Live && !modes.KtsRegistered)
        {
            throw new InvalidOperationException(
                "Ürünün KTS/DHBS tescili tamamlanmadan canlı (Live) moda geçilemez. " +
                "Tescil sonrası sistem yöneticisi Integrations:Enabiz:KtsRegistered bayrağını açmalıdır.");
        }

        // Şifre boş bırakıldıysa mevcut şifre korunur (ekranda geri gösterilmez).
        var existing = await store.GetAsync(tenantId, EnabizModeResolver.IntegrationKey, ct);
        var existingSettings = ParseExisting(existing?.SettingsJson);

        var payload = new EnabizTenantSettings(
            CkysCode: Coalesce(request.CkysCode, existingSettings?.CkysCode),
            UssUsername: Coalesce(request.UssUsername, existingSettings?.UssUsername),
            UssPassword: string.IsNullOrWhiteSpace(request.UssPassword)
                ? existingSettings?.UssPassword
                : request.UssPassword,
            ApplicationCode: Coalesce(request.ApplicationCode, existingSettings?.ApplicationCode),
            Mode: request.Mode);

        await store.UpsertAsync(
            tenantId,
            EnabizModeResolver.IntegrationKey,
            existing?.ProviderKey ?? DefaultProviderKey,
            request.Mode == EnabizMode.Live ? "Live" : "Test",
            JsonSerializer.Serialize(payload),
            isEnabled: request.Mode != EnabizMode.Disabled,
            ct);

        // ÇKYS kodu klinik kaydında da tutulur (paket başlığı oradan da okunabiliyor).
        if (!string.IsNullOrWhiteSpace(payload.CkysCode) && tenant.ClinicId is { } clinicId)
        {
            var clinic = await db.Clinics.FirstOrDefaultAsync(c => c.Id == clinicId, ct);
            if (clinic is not null) clinic.CkysCode = payload.CkysCode;
            await db.SaveChangesAsync(ct);
        }

        return Map(await modes.ResolveAsync(tenantId, ct));
    }

    private EnabizSettingsDto Map(EnabizModeSnapshot mode) => new(
        mode.Mode,
        mode.CkysCode,
        mode.UssUsername,
        mode.ApplicationCode,
        mode.HasPassword,
        mode.KtsRegistered,
        CanGoLive: mode.KtsRegistered);

    private static EnabizTenantSettings? ParseExisting(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<EnabizTenantSettings>(
                json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Coalesce(string? incoming, string? existing) =>
        string.IsNullOrWhiteSpace(incoming) ? existing : incoming.Trim();

    private long RequireTenantId() => tenant.TenantId
        ?? throw new InvalidOperationException("Tenant bağlamı kurulmadan e-Nabız ayarları okunamaz.");
}
