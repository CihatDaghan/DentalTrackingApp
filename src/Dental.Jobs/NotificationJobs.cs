using Dental.Application.Abstractions;
using Dental.Application.Notifications;
using Dental.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dental.Jobs;

/// <summary>
/// Bildirim üreten zamanlanmış işler. Olay bazlı bildirimler (randevu, tahsilat, e-belge hatası)
/// ilgili servislerde anında üretilir; burada yalnız TARAMA gerektiren durumlar vardır.
///
/// Diğer job sınıflarıyla aynı kural: her kiracı için ayrı tenant scope açılır
/// (<see cref="ITenantScopeFactory"/>), aksi hâlde global query filter'lar boş döner.
/// </summary>
public sealed class NotificationJobs(
    IServiceScopeFactory scopeFactory,
    ITenantScopeFactory tenantScopes,
    ILogger<NotificationJobs> logger)
{
    /// <summary>TR 08:00 = UTC 05:00 — mesai başında tek uyarı.</summary>
    public const string LowStockCron = "0 5 * * *";
    public const string LowStockJobId = "low-stock-notification";

    /// <summary>Kritik seviyenin altına düşen stok kalemleri için günlük tek bildirim.</summary>
    public async Task ScanLowStockAsync(CancellationToken ct = default)
    {
        foreach (var tenantId in await GetTenantIdsAsync(ct))
        {
            ct.ThrowIfCancellationRequested();
            using var scope = tenantScopes.CreateScope(tenantId);
            try
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var items = await db.StockItems.AsNoTracking()
                    .Where(i => i.IsActive && i.CurrentQty <= i.MinQty)
                    .OrderBy(i => i.Name)
                    .Select(i => new { i.Name, i.CurrentQty, i.Unit })
                    .Take(20)
                    .ToListAsync(ct);
                if (items.Count == 0) continue;

                var body = string.Join(", ", items.Select(i => $"{i.Name} ({i.CurrentQty:0.##} {i.Unit})"));
                await scope.ServiceProvider.GetRequiredService<INotificationService>()
                    .PublishAsync(new NotificationCreateRequest(
                        NotificationEvents.StockLow,
                        $"{items.Count} stok kalemi kritik seviyede",
                        body,
                        LinkPath: "/stock?filter=low",
                        TenantId: tenantId), ct);

                logger.LogInformation("Düşük stok bildirimi üretildi. TenantId={TenantId} Kalem={Count}",
                    tenantId, items.Count);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Düşük stok taraması başarısız. TenantId={TenantId}", tenantId);
            }
        }
    }

    private async Task<List<long>> GetTenantIdsAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.Tenants.AsNoTracking().Select(t => t.Id).ToListAsync(ct);
    }
}
