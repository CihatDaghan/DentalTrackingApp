using Dental.Application.Abstractions;
using Dental.Application.Messaging;
using Dental.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dental.Infrastructure.Messaging;

/// <summary>
/// Meta WhatsApp webhook işleyicisi. İstek anonimdir ve tenant claim'i taşımaz:
/// teslim durumları, mesajın ProviderMessageId'sinden kiracıya çözülür (IgnoreQueryFilters ile
/// YALNIZ id/tenant projeksiyonu), yazma işi tenant scope'unda yapılır.
/// Gelen (kullanıcıdan) mesajlar bu aşamada yalnız loglanır — konuşma penceresi takibi H aşamasında.
/// </summary>
public interface IWhatsAppWebhookService
{
    Task<int> HandleAsync(WaWebhookEvent webhookEvent, CancellationToken ct = default);
}

public sealed class WhatsAppWebhookService(
    AppDbContext db,
    ITenantScopeFactory scopeFactory,
    ILogger<WhatsAppWebhookService> logger) : IWhatsAppWebhookService
{
    public async Task<int> HandleAsync(WaWebhookEvent webhookEvent, CancellationToken ct = default)
    {
        var applied = 0;

        foreach (var status in webhookEvent.StatusUpdates)
        {
            ct.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(status.ProviderMessageId)) continue;

            var head = await db.OutboundMessages.IgnoreQueryFilters().AsNoTracking()
                .Where(m => m.ProviderMessageId == status.ProviderMessageId && !m.IsDeleted)
                .Select(m => new { m.Id, m.TenantId })
                .FirstOrDefaultAsync(ct);
            if (head is null)
            {
                logger.LogDebug("WhatsApp durumu eşleşen mesaj bulunamadı. SağlayıcıId={Id}",
                    status.ProviderMessageId);
                continue;
            }

            using var scope = scopeFactory.CreateScope(head.TenantId);
            var dispatcher = scope.ServiceProvider.GetRequiredService<IMessageDispatcher>();
            if (await dispatcher.ApplyDeliveryStatusAsync(
                    status.ProviderMessageId, status.Status, status.TimestampUtc,
                    status.ErrorDetail ?? status.ErrorCode, ct))
                applied++;
        }

        foreach (var incoming in webhookEvent.IncomingMessages)
        {
            logger.LogInformation(
                "WhatsApp gelen mesaj. From={From} Tip={Type} Zaman={At} Metin={Text}",
                incoming.From, incoming.Type, incoming.TimestampUtc, incoming.Text);
        }

        return applied;
    }
}
