using Dental.Application.Abstractions;
using Dental.Application.Enabiz;
using Dental.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dental.Jobs;

/// <summary>
/// e-Nabız / USS zamanlanmış işleri.
///
/// KRİTİK: Hangfire job'ları HTTP isteği bağlamı olmadan koşar; global query filter'lar bu yüzden
/// boş sonuç döndürür ya da yazma reddedilir. Her kiracı için <see cref="ITenantScopeFactory"/> ile
/// AYRI bir DI scope açılır ve iş o scope içinde yürütülür (EDocumentJobs ile aynı kalıp).
/// </summary>
public sealed class EnabizJobs(
    IServiceScopeFactory scopeFactory,
    ITenantScopeFactory tenantScopes,
    ILogger<EnabizJobs> logger)
{
    public const string QueueCron = "*/1 * * * *";
    public const string ReconcileCron = "30 23 * * *";
    public const string SkrsSyncCron = "0 4 * * *";

    public const string QueueJobId = "enabiz-queue";
    public const string ReconcileJobId = "enabiz-reconcile";
    public const string SkrsSyncJobId = "skrs-sync";

    /// <summary>
    /// Kuyruğu işler (*/1 dk). Önce Held→Queued geri doldurma yapılır: kiracı canlıya/teste
    /// geçtiğinde bekleyen paketler kendiliğinden akmaya başlasın diye.
    /// </summary>
    public Task DispatchQueuedAsync(CancellationToken ct = default) =>
        ForEachTenantAsync("EnabizQueue", async (dispatcher, tenantId) =>
        {
            var backfilled = await dispatcher.BackfillHeldAsync(batchSize: 200, ct);
            if (backfilled > 0)
                logger.LogInformation("e-Nabız geri doldurma. TenantId={TenantId} Adet={Count}",
                    tenantId, backfilled);

            var sent = await dispatcher.DispatchQueuedAsync(batchSize: 50, ct);
            if (sent > 0)
                logger.LogInformation("e-Nabız kuyruğu işlendi. TenantId={TenantId} Gönderim={Count}",
                    tenantId, sent);
        }, ct);

    /// <summary>405 günlük mutabakat (her gün 23:30): eksik kalanlar yeniden kuyruğa alınır.</summary>
    public Task ReconcileAsync(CancellationToken ct = default) =>
        ForEachTenantAsync("EnabizReconcile", async (dispatcher, tenantId) =>
        {
            var requeued = await dispatcher.ReconcileAsync(date: null, ct);
            if (requeued > 0)
                logger.LogInformation("e-Nabız mutabakatı. TenantId={TenantId} Yeniden={Count}",
                    tenantId, requeued);
        }, ct);

    /// <summary>
    /// SKRS kod setlerini tazeler (günlük 04:00). Kod setleri GLOBAL'dir; ilk uygun kiracının
    /// kimliğiyle bir kez çekilir — her kiracı için tekrar indirmek gereksiz yüktür.
    /// </summary>
    public async Task SyncSkrsAsync(CancellationToken ct = default)
    {
        foreach (var tenantId in await GetTenantIdsAsync(ct))
        {
            ct.ThrowIfCancellationRequested();
            using var scope = tenantScopes.CreateScope(tenantId);
            var codes = scope.ServiceProvider.GetRequiredService<ISkrsCodeService>();
            try
            {
                var count = await codes.SyncAsync(ct);
                if (count > 0)
                {
                    logger.LogInformation("SKRS kod setleri tazelendi. TenantId={TenantId} Kayıt={Count}",
                        tenantId, count);
                    return;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "SKRS senkronu başarısız. TenantId={TenantId}", tenantId);
            }
        }

        logger.LogWarning("SKRS kod setleri hiçbir kiracının kimliğiyle tazelenemedi.");
    }

    // ---- Ortak kiracı döngüsü ----

    private async Task ForEachTenantAsync(
        string jobName, Func<IEnabizDispatcher, long, Task> action, CancellationToken ct)
    {
        foreach (var tenantId in await GetTenantIdsAsync(ct))
        {
            ct.ThrowIfCancellationRequested();
            using var scope = tenantScopes.CreateScope(tenantId);
            try
            {
                var dispatcher = scope.ServiceProvider.GetRequiredService<IEnabizDispatcher>();
                await action(dispatcher, tenantId);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Bir kiracının hatası diğerlerinin işini durdurmaz.
                logger.LogError(ex, "{Job} kiracı için başarısız. TenantId={TenantId}", jobName, tenantId);
            }
        }
    }

    /// <summary>Kiracı listesi tenant bağlamı DIŞINDA okunur (Tenant tablosu kiracıya ait değildir).</summary>
    private async Task<List<long>> GetTenantIdsAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Tenants.AsNoTracking().Select(t => t.Id).ToListAsync(ct);
    }
}
