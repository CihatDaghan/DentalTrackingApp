using Dental.Application.Abstractions;
using Dental.Application.Enabiz;
using Dental.Domain.Common;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Persistence;
using Dental.Integrations.Enabiz.PacketBuilders;
using Dental.Integrations.Enabiz.Schemas;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Dental.Infrastructure.Enabiz;

/// <summary>
/// e-Nabız gönderim motoru: bağımlılık sırası + durum makinesi + yeniden deneme.
///
/// <para><b>Bağımlılık kuralı:</b> ziyaretin 101 paketi kabul edilip SysTakipNo alınmadan
/// 102/103/203 gönderilmez — bunlar zorunlu <c>HASTA_TAKIP_BILGISI/SYSTakipNo</c> alanını 101'in
/// yanıtından alır. Bağımlılığı karşılanmamış paket kuyrukta bekler, hata saymaz.</para>
///
/// <para><b>Yeniden deneme (design-2 §1.0-b):</b> taşıma hatasında 1dk → 5dk → 30dk → 2sa → 12sa;
/// 6. denemeden sonra <see cref="EnabizSubmissionState.ManualReview"/>. USS'nin iş reddi
/// (<see cref="EnabizSubmissionState.Rejected"/>) YENİDEN DENENMEZ — aynı veri aynı reddi alır;
/// düzeltme kuyruğuna düşer ve elle müdahale ister.</para>
/// </summary>
public sealed class EnabizDispatcher(
    AppDbContext db,
    ITenantContext tenant,
    IClock clock,
    IIntegrationProviderFactory providerFactory,
    EnabizPacketFactory packets,
    EnabizModeResolver modes,
    Application.Notifications.INotificationService notifications,
    ILogger<EnabizDispatcher> logger) : IEnabizDispatcher
{
    /// <summary>Artan yeniden deneme aralıkları; son eleman aşıldığında ManualReview'a düşülür.</summary>
    internal static readonly TimeSpan[] RetryBackoff =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(30),
        TimeSpan.FromHours(2),
        TimeSpan.FromHours(12),
    ];

    public const int MaxAttempts = 6;

    public async Task<EnabizSubmissionState> DispatchAsync(long submissionId, CancellationToken ct = default)
    {
        var tenantId = RequireTenantId();
        var submission = await db.EnabizSubmissions.FirstOrDefaultAsync(s => s.Id == submissionId, ct)
            ?? throw new KeyNotFoundException("e-Nabız gönderim kaydı bulunamadı.");

        var mode = await modes.ResolveAsync(tenantId, ct);
        if (!mode.ShouldSend)
        {
            logger.LogDebug("Gönderim atlandı, mod uygun değil. Id={Id} Mod={Mode}", submission.Id, mode.Mode);
            return submission.State;
        }

        if (submission.State is not (EnabizSubmissionState.Queued or EnabizSubmissionState.Held))
            return submission.State;

        // Bağımlılık: 101 kabul edilip takip numarası gelmeden bağımlı paket gönderilemez.
        var parentTakipNo = await ResolveDependencyAsync(submission, ct);
        if (parentTakipNo is DependencyResult.NotReady)
        {
            logger.LogDebug("Bağımlılık henüz karşılanmadı, paket kuyrukta bekliyor. Id={Id}", submission.Id);
            return submission.State;
        }

        if (parentTakipNo is DependencyResult.Failed failed)
        {
            Fail(submission, "DEP", failed.Reason, permanent: true);
            await db.SaveChangesAsync(ct);
            return submission.State;
        }

        var sysTakipNo = ((DependencyResult.Ready)parentTakipNo).SysTakipNo;

        // Paket gönderim anında yeniden üretilir: Held'de bekleyen paket güncel SKRS koduyla gitsin.
        if (submission.RegenerateOnSend || string.IsNullOrWhiteSpace(submission.PayloadXml))
        {
            try
            {
                submission.PayloadXml = await packets.BuildAsync(submission, sysTakipNo, ct);
            }
            catch (Exception ex) when (ex is EnabizPacketException or EnabizPacketValidationException)
            {
                // Alan/şema hatası kalıcıdır; yeniden denemek aynı hatayı verir.
                Fail(submission, "VALIDATION", ex.Message, permanent: true);
                await db.SaveChangesAsync(ct);
                return submission.State;
            }
        }

        submission.State = EnabizSubmissionState.Sending;
        await db.SaveChangesAsync(ct);

        var resolved = await providerFactory.ResolveAsync<IEnabizClient>(tenantId, ct);
        var packet = new EnabizPacket(
            (short)submission.PacketType, submission.PayloadXml!, submission.FacilityCode, sysTakipNo);

        EnabizSendResult result;
        try
        {
            result = await resolved.Instance.SendPacketAsync(packet, ct);
        }
        catch (Exception ex) when (ex is EnabizClientException or HttpRequestException or TaskCanceledException)
        {
            // Taşıma/altyapı hatası → geçici, artan aralıkla yeniden denenir.
            logger.LogWarning(ex, "e-Nabız gönderimi başarısız (geçici). Id={Id} Sürücü={Provider}",
                submission.Id, resolved.ProviderKey);
            Fail(submission, "TRANSPORT", ex.Message, permanent: false);
            await db.SaveChangesAsync(ct);
            return submission.State;
        }

        if (!result.Accepted)
        {
            // USS iş kuralıyla reddetti: yeniden denemek anlamsız, düzeltme kuyruğuna düşer.
            logger.LogWarning("e-Nabız paketi reddedildi. Id={Id} Kod={Code} Mesaj={Message}",
                submission.Id, result.ErrorCode, result.ErrorMessage);
            submission.State = EnabizSubmissionState.Rejected;
            submission.LastErrorCode = Truncate(result.ErrorCode, 20);
            submission.LastErrorMessage = Truncate(result.ErrorMessage, 2000);
            submission.NextAttemptAtUtc = null;
            submission.AttemptCount++;
            await MarkTreatmentAsync(submission, EnabizStatus.Rejected, ct);
            await db.SaveChangesAsync(ct);
            await notifications.PublishAsync(new Application.Notifications.NotificationCreateRequest(
                Application.Notifications.NotificationEvents.EnabizRejected,
                "e-Nabız paketi reddedildi",
                $"Paket {(short)submission.PacketType} — {result.ErrorCode}: {result.ErrorMessage}",
                LinkPath: $"/enabiz/submissions/{submission.Id}",
                TenantId: submission.TenantId), ct);
            return submission.State;
        }

        submission.State = EnabizSubmissionState.Accepted;
        submission.SysTakipNo = result.SysTakipNo;
        submission.SentAtUtc = clock.UtcNow;
        submission.NextAttemptAtUtc = null;
        submission.LastErrorCode = null;
        submission.LastErrorMessage = null;
        submission.AttemptCount++;

        // 101'in takip numarası ziyarete yazılır; bağımlı paketler buradan okur.
        if (submission.PacketType == EnabizPacketType.HastaKayit101 &&
            !string.IsNullOrWhiteSpace(result.SysTakipNo) &&
            submission.VisitId is { } visitId)
        {
            var visit = await db.Visits.FirstOrDefaultAsync(v => v.Id == visitId, ct);
            if (visit is not null) visit.SysTakipNo = result.SysTakipNo;
        }

        await MarkTreatmentAsync(submission, EnabizStatus.Accepted, ct);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("e-Nabız paketi kabul edildi. Id={Id} Paket={PacketType} TakipNo={TakipNo}",
            submission.Id, submission.PacketType, result.SysTakipNo);
        return submission.State;
    }

    public async Task<int> DispatchQueuedAsync(int batchSize = 50, CancellationToken ct = default)
    {
        var tenantId = RequireTenantId();
        var mode = await modes.ResolveAsync(tenantId, ct);
        if (!mode.ShouldSend) return 0;

        var now = clock.UtcNow;
        // Ziyaret sırası + kimlik sırası: 101 her zaman bağımlılarından önce gelir.
        var ids = await db.EnabizSubmissions.AsNoTracking()
            .Where(s => s.State == EnabizSubmissionState.Queued &&
                        (s.NextAttemptAtUtc == null || s.NextAttemptAtUtc <= now))
            .OrderBy(s => s.VisitId)
            .ThenBy(s => s.Id)
            .Take(batchSize)
            .Select(s => s.Id)
            .ToListAsync(ct);

        var processed = 0;
        foreach (var id in ids)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await DispatchAsync(id, ct);
                processed++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Tek paketin hatası kuyruğun tamamını durdurmasın.
                logger.LogError(ex, "e-Nabız kuyruk gönderiminde beklenmeyen hata. Id={Id}", id);
                db.ChangeTracker.Clear();
            }
        }

        return processed;
    }

    public async Task<int> BackfillHeldAsync(int batchSize = 200, CancellationToken ct = default)
    {
        var tenantId = RequireTenantId();
        var mode = await modes.ResolveAsync(tenantId, ct);
        if (!mode.ShouldSend) return 0;

        // Ziyaret sırasına göre: eski başvurular önce, 101'ler bağımlılarından önce.
        var held = await db.EnabizSubmissions
            .Where(s => s.State == EnabizSubmissionState.Held)
            .OrderBy(s => s.VisitId)
            .ThenBy(s => s.Id)
            .Take(batchSize)
            .ToListAsync(ct);

        if (held.Count == 0) return 0;

        var now = clock.UtcNow;
        foreach (var submission in held)
        {
            submission.State = EnabizSubmissionState.Queued;
            submission.NextAttemptAtUtc = now;
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Bekleyen e-Nabız paketleri kuyruğa alındı. TenantId={TenantId} Adet={Count}",
            tenantId, held.Count);
        return held.Count;
    }

    /// <summary>
    /// 405 mutabakatı. USS'de görünmeyen ama bizde Accepted duran paketler yeniden kuyruğa alınır.
    ///
    /// <para><b>Sınır:</b> 405 yanıtının kayıt listesi biçimi kimlik doğrulanmış bir çağrı
    /// yapılamadığı için doğrulanamadı; bu yüzden şimdilik yalnız <b>takip numarası olmayan</b>
    /// (yani kabul edildiği hâlde numara dönmemiş) paketler yeniden kuyruğa alınır — bu, yanıt
    /// biçiminden bağımsız olarak güvenli ve doğru bir mutabakattır.</para>
    /// </summary>
    public async Task<int> ReconcileAsync(DateOnly? date = null, CancellationToken ct = default)
    {
        var tenantId = RequireTenantId();
        var mode = await modes.ResolveAsync(tenantId, ct);
        if (!mode.ShouldSend) return 0;

        var target = date ?? TrTime.ToLocalDate(clock.UtcNow).AddDays(-1);
        var (startUtc, endUtc) = TrTime.DayRangeUtc(target);

        var suspicious = await db.EnabizSubmissions
            .Where(s => s.State == EnabizSubmissionState.Accepted &&
                        s.SentAtUtc >= startUtc && s.SentAtUtc < endUtc &&
                        (s.SysTakipNo == null || s.SysTakipNo == ""))
            .Take(200)
            .ToListAsync(ct);

        if (suspicious.Count == 0) return 0;

        var now = clock.UtcNow;
        foreach (var submission in suspicious)
        {
            submission.State = EnabizSubmissionState.Queued;
            submission.NextAttemptAtUtc = now;
            submission.LastErrorCode = "RECONCILE";
            submission.LastErrorMessage =
                $"{target:yyyy-MM-dd} mutabakatında takip numarası bulunamadı; yeniden gönderiliyor.";
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("e-Nabız mutabakatı: {Count} paket yeniden kuyruğa alındı. Tarih={Date}",
            suspicious.Count, target);
        return suspicious.Count;
    }

    public async Task<EnabizSubmissionState> RetryAsync(long submissionId, CancellationToken ct = default)
    {
        var submission = await db.EnabizSubmissions.FirstOrDefaultAsync(s => s.Id == submissionId, ct)
            ?? throw new KeyNotFoundException("e-Nabız gönderim kaydı bulunamadı.");

        if (submission.State == EnabizSubmissionState.Accepted)
            throw new InvalidOperationException("Kabul edilmiş paket yeniden gönderilemez (mükerrer kayıt oluşur).");

        submission.State = EnabizSubmissionState.Queued;
        submission.AttemptCount = 0;
        submission.NextAttemptAtUtc = clock.UtcNow;
        submission.LastErrorCode = null;
        submission.LastErrorMessage = null;
        await db.SaveChangesAsync(ct);

        return await DispatchAsync(submissionId, ct);
    }

    // ---- Bağımlılık ----

    private abstract record DependencyResult
    {
        public sealed record Ready(string? SysTakipNo) : DependencyResult;
        public sealed record NotReady : DependencyResult;
        public sealed record Failed(string Reason) : DependencyResult;
    }

    private async Task<DependencyResult> ResolveDependencyAsync(
        EnabizSubmission submission, CancellationToken ct)
    {
        // 101 kendisi bağımsızdır: takip numarasını USS üretir.
        if (submission.PacketType == EnabizPacketType.HastaKayit101)
            return new DependencyResult.Ready(null);

        if (submission.DependsOnSubmissionId is not { } parentId)
        {
            // Bağımlılık kaydı yoksa ziyaretin takip numarasına bakılır.
            var visitTakipNo = submission.VisitId is { } vid
                ? await db.Visits.Where(v => v.Id == vid).Select(v => v.SysTakipNo).FirstOrDefaultAsync(ct)
                : null;
            return string.IsNullOrWhiteSpace(visitTakipNo)
                ? new DependencyResult.NotReady()
                : new DependencyResult.Ready(visitTakipNo);
        }

        var parent = await db.EnabizSubmissions.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == parentId, ct);

        if (parent is null)
            return new DependencyResult.Failed("Bağımlı olunan 101 paketi bulunamadı.");

        if (parent.State is EnabizSubmissionState.Rejected or EnabizSubmissionState.GaveUp)
        {
            return new DependencyResult.Failed(
                $"Bağımlı olunan 101 paketi {parent.State} durumunda; bu paket gönderilemez.");
        }

        if (parent.State != EnabizSubmissionState.Accepted)
            return new DependencyResult.NotReady();

        return string.IsNullOrWhiteSpace(parent.SysTakipNo)
            ? new DependencyResult.Failed("101 kabul edildi ama SysTakipNo dönmedi.")
            : new DependencyResult.Ready(parent.SysTakipNo);
    }

    // ---- Durum makinesi ----

    private void Fail(EnabizSubmission submission, string code, string? message, bool permanent)
    {
        submission.LastErrorCode = Truncate(code, 20);
        submission.LastErrorMessage = Truncate(message, 2000);
        submission.AttemptCount++;

        if (permanent || submission.AttemptCount >= MaxAttempts)
        {
            submission.NextAttemptAtUtc = null;
            submission.State = EnabizSubmissionState.ManualReview;
            return;
        }

        var backoff = RetryBackoff[Math.Min(submission.AttemptCount - 1, RetryBackoff.Length - 1)];
        submission.NextAttemptAtUtc = clock.UtcNow.Add(backoff);
        submission.State = EnabizSubmissionState.Queued;
    }

    /// <summary>Tedavi kaydının e-Nabız durumunu yansıtır (liste ekranında rozet).</summary>
    private async Task MarkTreatmentAsync(EnabizSubmission submission, EnabizStatus status, CancellationToken ct)
    {
        if (submission.TreatmentRecordId is not { } recordId) return;
        var record = await db.TreatmentRecords.FirstOrDefaultAsync(t => t.Id == recordId, ct);
        if (record is not null) record.EnabizStatus = status;
    }

    private long RequireTenantId() => tenant.TenantId
        ?? throw new InvalidOperationException("Tenant bağlamı kurulmadan e-Nabız gönderimi yapılamaz.");

    private static string? Truncate(string? value, int max) =>
        value is null ? null : value.Length <= max ? value : value[..max];
}
