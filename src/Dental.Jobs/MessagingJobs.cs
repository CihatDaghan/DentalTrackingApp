using Dental.Application.Abstractions;
using Dental.Application.Messaging;
using Dental.Application.Payments;
using Dental.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dental.Jobs;

/// <summary>
/// Mesajlaşma, otomasyon ve ödeme linki zamanlanmış işleri.
///
/// KRİTİK (EDocumentJobs ile aynı kural): Hangfire job'ları HTTP bağlamı olmadan koşar; global
/// query filter'lar bu yüzden boş sonuç döndürür ya da yazma reddedilir. Her kiracı için
/// <see cref="ITenantScopeFactory"/> ile AYRI bir DI scope açılır ve iş o scope içinde yürütülür.
///
/// Cron ifadeleri UTC'dir; TR yerel saatleri (09:00 / 10:00) UTC+3 çıkarılarak yazılmıştır.
/// Günlük işler ayrıca kuralın SendAtLocalTime alanına göre saat penceresi kontrolü yapmaz —
/// zamanlama tek yerde (cron) tutulur, kural yalnız açık/kapalı ve şablon bilgisini taşır.
/// </summary>
public sealed class MessagingJobs(
    IServiceScopeFactory scopeFactory,
    ITenantScopeFactory tenantScopes,
    ILogger<MessagingJobs> logger)
{
    public const string DispatchCron = "*/1 * * * *";
    public const string AppointmentReminderCron = "*/5 * * * *";
    /// <summary>TR 09:00 = UTC 06:00.</summary>
    public const string BirthdayCron = "0 6 * * *";
    /// <summary>TR 10:00 = UTC 07:00.</summary>
    public const string PaymentOverdueCron = "0 7 * * *";
    /// <summary>TR 10:00 = UTC 07:00 (doğum günü işinden sonra koşsun diye 10 dk kaydırıldı).</summary>
    public const string RecallCron = "10 7 * * *";
    public const string PaymentLinkExpiryCron = "5 * * * *";

    public const string DispatchJobId = "message-dispatch";
    public const string AppointmentReminderJobId = "appointment-reminder";
    public const string BirthdayJobId = "birthday-greeting";
    public const string PaymentOverdueJobId = "payment-overdue-reminder";
    public const string RecallJobId = "recall-reminder";
    public const string PaymentLinkExpiryJobId = "payment-link-expiry";

    /// <summary>Bekleyen outbox mesajlarını sürücülere verir (*/1 dk).</summary>
    public Task DispatchPendingAsync(CancellationToken ct = default) =>
        ForEachTenantAsync("MessageDispatch", async (sp, tenantId) =>
        {
            var sent = await sp.GetRequiredService<IMessageDispatcher>().DispatchPendingAsync(200, ct);
            if (sent > 0)
                logger.LogInformation("Mesaj kuyruğu işlendi. TenantId={TenantId} Gönderim={Count}", tenantId, sent);
        }, ct);

    /// <summary>Kuralın offset penceresine giren randevular için hatırlatma üretir (*/5 dk).</summary>
    public Task QueueAppointmentRemindersAsync(CancellationToken ct = default) =>
        ForEachTenantAsync("AppointmentReminder", async (sp, tenantId) =>
        {
            var queued = await sp.GetRequiredService<IMessageAutomationService>()
                .QueueAppointmentRemindersAsync(ct);
            if (queued > 0)
                logger.LogInformation("Randevu hatırlatmaları üretildi. TenantId={TenantId} Adet={Count}",
                    tenantId, queued);
        }, ct);

    /// <summary>Doğum günü mesajları (günlük TR 09:00). TİCARİ mesajdır: izin filtresi uygulanır.</summary>
    public Task QueueBirthdayGreetingsAsync(CancellationToken ct = default) =>
        ForEachTenantAsync("BirthdayGreeting", async (sp, tenantId) =>
        {
            var queued = await sp.GetRequiredService<IMessageAutomationService>().QueueBirthdayGreetingsAsync(ct);
            if (queued > 0)
                logger.LogInformation("Doğum günü mesajları üretildi. TenantId={TenantId} Adet={Count}",
                    tenantId, queued);
        }, ct);

    /// <summary>Vadesi geçmiş taksitler için ödeme hatırlatması (günlük TR 10:00).</summary>
    public Task QueuePaymentOverdueRemindersAsync(CancellationToken ct = default) =>
        ForEachTenantAsync("PaymentOverdue", async (sp, tenantId) =>
        {
            var queued = await sp.GetRequiredService<IMessageAutomationService>()
                .QueuePaymentOverdueRemindersAsync(ct);
            if (queued > 0)
                logger.LogInformation("Gecikmiş ödeme hatırlatmaları üretildi. TenantId={TenantId} Adet={Count}",
                    tenantId, queued);
        }, ct);

    /// <summary>Tarihi yaklaşan kontrol (recall) planları için hatırlatma (günlük TR 10:10).</summary>
    public Task QueueRecallRemindersAsync(CancellationToken ct = default) =>
        ForEachTenantAsync("RecallReminder", async (sp, tenantId) =>
        {
            var queued = await sp.GetRequiredService<IMessageAutomationService>().QueueRecallRemindersAsync(ct);
            if (queued > 0)
                logger.LogInformation("Kontrol hatırlatmaları üretildi. TenantId={TenantId} Adet={Count}",
                    tenantId, queued);
        }, ct);

    /// <summary>Süresi geçen ödeme linklerini kapatır (saatlik).</summary>
    public Task ExpirePaymentLinksAsync(CancellationToken ct = default) =>
        ForEachTenantAsync("PaymentLinkExpiry", async (sp, tenantId) =>
        {
            var expired = await sp.GetRequiredService<IPaymentLinkService>().ExpireStaleAsync(ct);
            if (expired > 0)
                logger.LogInformation("Süresi geçen ödeme linkleri kapatıldı. TenantId={TenantId} Adet={Count}",
                    tenantId, expired);
        }, ct);

    // ---- Ortak kiracı döngüsü ----

    private async Task ForEachTenantAsync(
        string jobName, Func<IServiceProvider, long, Task> action, CancellationToken ct)
    {
        foreach (var tenantId in await GetTenantIdsAsync(ct))
        {
            ct.ThrowIfCancellationRequested();
            using var scope = tenantScopes.CreateScope(tenantId);
            try
            {
                await action(scope.ServiceProvider, tenantId);
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
