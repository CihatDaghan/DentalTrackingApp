using Dental.Application.Abstractions;
using Dental.Application.Invoices;
using Dental.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dental.Jobs;

/// <summary>
/// e-Belge zamanlanmış işleri.
///
/// KRİTİK: Hangfire job'ları HTTP isteği bağlamı olmadan koşar; global query filter'lar bu yüzden
/// boş sonuç döndürür ya da yazma reddedilir. Her kiracı için <see cref="ITenantScopeFactory"/> ile
/// AYRI bir DI scope açılır ve iş o scope içinde yürütülür (denetim (c)-5 düzeltmesi).
/// </summary>
public sealed class EDocumentJobs(
    IServiceScopeFactory scopeFactory,
    ITenantScopeFactory tenantScopes,
    ILogger<EDocumentJobs> logger)
{
    public const string DispatchCron = "*/1 * * * *";
    public const string StatusPollCron = "*/10 * * * *";
    public const string TaxpayerSyncCron = "0 5 * * *";

    public const string DispatchJobId = "edocument-dispatch";
    public const string StatusPollJobId = "edocument-status-poll";
    public const string TaxpayerSyncJobId = "gib-taxpayer-sync";

    /// <summary>Kuyruktaki belgeleri entegratöre gönderir (*/1 dk).</summary>
    public Task DispatchQueuedAsync(CancellationToken ct = default) =>
        ForEachTenantAsync("EDocumentDispatch", async (dispatcher, tenantId) =>
        {
            var sent = await dispatcher.DispatchQueuedAsync(batchSize: 50, ct);
            if (sent > 0)
                logger.LogInformation("e-belge kuyruğu işlendi. TenantId={TenantId} Gönderim={Count}", tenantId, sent);
        }, ct);

    /// <summary>Entegratörde bekleyen belgelerin nihai durumunu yoklar (*/10 dk).</summary>
    public Task PollStatusesAsync(CancellationToken ct = default) =>
        ForEachTenantAsync("EDocumentStatusPoll", async (dispatcher, tenantId) =>
        {
            var updated = await dispatcher.PollStatusesAsync(batchSize: 100, ct);
            if (updated > 0)
                logger.LogInformation("e-belge durumları güncellendi. TenantId={TenantId} Değişen={Count}",
                    tenantId, updated);
        }, ct);

    /// <summary>
    /// GİB mükellef aynasını tazeler (günlük 05:00). Ayna GLOBAL'dir; ilk uygun kiracının
    /// sürücüsüyle bir kez indirilir — her kiracı için tekrar indirmek gereksiz yüktür.
    /// </summary>
    public async Task SyncGibTaxpayersAsync(CancellationToken ct = default)
    {
        foreach (var tenantId in await GetTenantIdsAsync(ct))
        {
            ct.ThrowIfCancellationRequested();
            using var scope = tenantScopes.CreateScope(tenantId);
            var dispatcher = scope.ServiceProvider.GetRequiredService<IEDocumentDispatcher>();
            try
            {
                var count = await dispatcher.SyncGibTaxpayersAsync(ct);
                if (count > 0)
                {
                    logger.LogInformation(
                        "GİB mükellef aynası tazelendi. TenantId={TenantId} Kayıt={Count}", tenantId, count);
                    return;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "GİB mükellef senkronu başarısız. TenantId={TenantId}", tenantId);
            }
        }

        logger.LogWarning("GİB mükellef aynası hiçbir kiracının sürücüsünden tazelenemedi.");
    }

    // ---- Ortak kiracı döngüsü ----

    private async Task ForEachTenantAsync(
        string jobName, Func<IEDocumentDispatcher, long, Task> action, CancellationToken ct)
    {
        foreach (var tenantId in await GetTenantIdsAsync(ct))
        {
            ct.ThrowIfCancellationRequested();
            using var scope = tenantScopes.CreateScope(tenantId);
            try
            {
                var dispatcher = scope.ServiceProvider.GetRequiredService<IEDocumentDispatcher>();
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
