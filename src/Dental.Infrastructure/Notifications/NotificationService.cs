using Dental.Application.Abstractions;
using Dental.Application.Common;
using Dental.Application.Notifications;
using Dental.Domain.Entities;
using Dental.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Dental.Infrastructure.Notifications;

/// <summary>
/// Uygulama içi bildirim (topbar zili).
///
/// <para><b>Yayın kapsamı:</b> <c>UserId = NULL</c> bildirimler kiracıdaki herkese görünür;
/// dolu olanlar yalnız o kullanıcıya. Okundu işareti kullanıcı bazlı tutulamayacağı için
/// (tek satır) yayın bildirimini okuyan ilk kullanıcı onu herkes için okundu yapar —
/// bilinçli sadeleştirme; kullanıcı bazlı okundu tablosu kapsam dışıdır.</para>
///
/// <para><b>Yan etki güvenliği:</b> <see cref="PublishAsync"/> hiçbir koşulda dışarı istisna
/// sızdırmaz. Bildirim üretimi iş akışına takılırsa randevu/tahsilat kaydı düşmemelidir.</para>
/// </summary>
public sealed class NotificationService(
    AppDbContext db,
    ITenantContext tenant,
    IClock clock,
    ILogger<NotificationService> logger) : INotificationService
{
    public async Task PublishAsync(NotificationCreateRequest request, CancellationToken ct = default)
    {
        try
        {
            var tenantId = request.TenantId ?? tenant.TenantId;
            if (tenantId is not { } id)
            {
                logger.LogDebug("Bildirim atlandı ({Event}): kiracı bağlamı yok.", request.EventType);
                return;
            }

            // Bildirim ana iş akışının SaveChanges'ine karışmasın diye AYRI bir ekleme yapılır;
            // çağıran kendi kaydını zaten kaydetmiş olur.
            db.Notifications.Add(new Notification
            {
                TenantId = id,
                UserId = request.UserId,
                EventType = request.EventType,
                Title = Trim(request.Title, 200) ?? request.EventType,
                Body = Trim(request.Body, 1000),
                LinkPath = Trim(request.LinkPath, 300),
            });
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Bildirim üretilemedi ({Event}).", request.EventType);
        }
    }

    public async Task<NotificationListDto> ListAsync(
        bool unreadOnly, int page, int pageSize, CancellationToken ct = default)
    {
        var userId = tenant.UserId;
        // Kendi bildirimlerim + kiracı geneli yayınlar.
        var source = db.Notifications.AsNoTracking()
            .Where(n => n.UserId == null || n.UserId == userId);
        if (unreadOnly) source = source.Where(n => n.ReadAtUtc == null);

        var request = new PageRequest(page, pageSize);
        var totalCount = await source.CountAsync(ct);
        var items = await source
            .OrderByDescending(n => n.CreatedAtUtc).ThenByDescending(n => n.Id)
            .Skip(request.Skip).Take(request.EffectivePageSize)
            .Select(n => new NotificationDto(
                n.Id, n.EventType, n.Title, n.Body, n.LinkPath, n.CreatedAtUtc, n.ReadAtUtc))
            .ToListAsync(ct);

        var unreadCount = await db.Notifications.AsNoTracking()
            .CountAsync(n => (n.UserId == null || n.UserId == userId) && n.ReadAtUtc == null, ct);

        return new NotificationListDto(
            new PagedResult<NotificationDto>(items, request.Page, request.EffectivePageSize, totalCount),
            unreadCount);
    }

    public async Task<int> MarkReadAsync(long id, CancellationToken ct = default)
    {
        var userId = tenant.UserId;
        var updated = await db.Notifications
            .Where(n => n.Id == id && n.ReadAtUtc == null && (n.UserId == null || n.UserId == userId))
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAtUtc, clock.UtcNow), ct);
        if (updated == 0 && !await db.Notifications.AnyAsync(n => n.Id == id, ct))
            throw new KeyNotFoundException("Bildirim bulunamadı.");
        return updated;
    }

    public Task<int> MarkAllReadAsync(CancellationToken ct = default)
    {
        var userId = tenant.UserId;
        return db.Notifications
            .Where(n => n.ReadAtUtc == null && (n.UserId == null || n.UserId == userId))
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAtUtc, clock.UtcNow), ct);
    }

    /// <summary>Kolon üst sınırına kırpar; null/boş değer null kalır.</summary>
    private static string? Trim(string? value, int max) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Length <= max ? value : value[..max];
}
