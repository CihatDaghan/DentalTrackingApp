using System.Text.Json;
using Dental.Application.Abstractions;
using Dental.Application.Messaging;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Dental.Infrastructure.Messaging;

/// <summary>
/// Outbox gönderim motoru (EDocumentDispatcher kalıbı).
///
/// Yeniden deneme: taşıma/altyapı hatası GEÇİCİ sayılır, AttemptCount artar ve NextAttemptAtUtc
/// 1dk → 5dk → 30dk → 2sa → 12sa uzar; 6. denemede Failed olur. Sağlayıcının iş reddi
/// (onaysız şablon, geçersiz numara, yetersiz kredi) KALICI sayılır, yeniden denenmez.
///
/// Fallback: WhatsApp gönderimi kalıcı olarak başarısızsa (ya da yeniden denemeler bittiyse)
/// kanal politikası WhatsAppFirstThenSms ise aynı içerik SMS olarak yeniden kuyruğa girer
/// (FallbackOfMessageId ile bağlanır). SMS son duraktır; ondan fallback üretilmez.
/// </summary>
public sealed class MessageDispatcher(
    AppDbContext db,
    ITenantContext tenant,
    IClock clock,
    IIntegrationProviderFactory providerFactory,
    ILogger<MessageDispatcher> logger) : IMessageDispatcher
{
    internal static readonly TimeSpan[] RetryBackoff =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(30),
        TimeSpan.FromHours(2),
        TimeSpan.FromHours(12),
    ];

    public const int MaxAttempts = 6;

    public async Task<OutboundMessageState> DispatchAsync(long messageId, CancellationToken ct = default)
    {
        var message = await db.OutboundMessages.FirstOrDefaultAsync(m => m.Id == messageId, ct)
            ?? throw new KeyNotFoundException("Mesaj bulunamadı.");

        if (message.State is not OutboundMessageState.Pending)
        {
            logger.LogDebug("Gönderim atlandı, durum uygun değil. Id={Id} Durum={State}", message.Id, message.State);
            return message.State;
        }

        message.State = OutboundMessageState.Sending;
        await db.SaveChangesAsync(ct);

        var tenantId = tenant.TenantId
            ?? throw new InvalidOperationException("Tenant bağlamı kurulmadan mesaj gönderilemez.");

        try
        {
            switch (message.Channel)
            {
                case MessageChannel.Sms:
                    await SendSmsAsync(message, tenantId, ct);
                    break;
                case MessageChannel.WhatsApp:
                    await SendWhatsAppAsync(message, tenantId, ct);
                    break;
                default:
                    // E-posta sürücüsü henüz yok; kayıt kaybolmasın diye Skipped'a düşer.
                    message.State = OutboundMessageState.Skipped;
                    message.SkipReason = MessageSkipReason.ChannelDisabled;
                    message.Error = "E-posta kanalı için sürücü tanımlı değil.";
                    break;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Mesaj gönderiminde beklenmeyen hata. Id={Id}", message.Id);
            await FailAsync(message, ex.Message, permanent: false, ct);
        }

        await db.SaveChangesAsync(ct);
        return message.State;
    }

    public async Task<int> DispatchPendingAsync(int batchSize = 100, CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        var ids = await db.OutboundMessages.AsNoTracking()
            .Where(m => m.State == OutboundMessageState.Pending
                        && m.ScheduledAtUtc <= now
                        && (m.NextAttemptAtUtc == null || m.NextAttemptAtUtc <= now))
            .OrderBy(m => m.Id)
            .Take(batchSize)
            .Select(m => m.Id)
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
                // Tek mesajın hatası kuyruğun tamamını durdurmasın.
                logger.LogError(ex, "Kuyruk gönderiminde beklenmeyen hata. Id={Id}", id);
                db.ChangeTracker.Clear();
            }
        }

        return processed;
    }

    public async Task<bool> ApplyDeliveryStatusAsync(
        string providerMessageId, string status, DateTime atUtc, string? error, CancellationToken ct = default)
    {
        var message = await db.OutboundMessages
            .FirstOrDefaultAsync(m => m.ProviderMessageId == providerMessageId, ct);
        if (message is null) return false;

        switch (status.ToLowerInvariant())
        {
            case "delivered":
            case "read":
                message.DeliveredAtUtc = atUtc;
                message.State = OutboundMessageState.Delivered;
                break;
            case "failed":
            case "undelivered":
                message.Error = Truncate(error ?? status);
                message.State = OutboundMessageState.Failed;
                break;
            case "sent":
                if (message.State == OutboundMessageState.Pending) message.State = OutboundMessageState.Sent;
                message.SentAtUtc ??= atUtc;
                break;
            default:
                return false;
        }

        await db.SaveChangesAsync(ct);
        logger.LogInformation("Mesaj teslim durumu işlendi. Id={Id} Durum={Status}", message.Id, status);
        return true;
    }

    // ---- Kanal gönderimleri ----

    private async Task SendSmsAsync(OutboundMessage message, long tenantId, CancellationToken ct)
    {
        var resolved = await providerFactory.ResolveAsync<ISmsProvider>(tenantId, ct);
        message.ProviderKey = resolved.ProviderKey;

        var header = await ClinicHeaderAsync(message, ct);
        var payload = new SmsMessage(
            PhoneNumbers.ToProviderFormat(message.ToAddress),
            message.RenderedBody,
            header,
            message.Kind == MessageKind.Commercial ? SmsKind.Commercial : SmsKind.Transactional,
            message.CorrelationId);

        SmsSendResult result;
        try
        {
            result = await resolved.Instance.SendAsync(payload, ct);
        }
        catch (Exception ex) when (ex is SmsProviderException or HttpRequestException or TaskCanceledException)
        {
            await FailAsync(message, ex.Message, permanent: false, ct);
            return;
        }

        if (!result.Success)
        {
            await FailAsync(message, result.Error, permanent: true, ct);
            return;
        }

        Succeed(message, result.ProviderMessageId, result.CreditCost);
    }

    private async Task SendWhatsAppAsync(OutboundMessage message, long tenantId, CancellationToken ct)
    {
        // Meta yalnız ONAYLI şablonla gönderime izin verir; onaysızsa denemek anlamsızdır.
        var waTemplate = await db.WhatsAppTemplates.AsNoTracking()
            .Where(t => t.TemplateKey == message.TemplateKey && t.MetaStatus == WaTemplateStatus.Approved)
            .OrderBy(t => t.Id)
            .FirstOrDefaultAsync(ct);

        if (waTemplate is null)
        {
            await FailAsync(message,
                $"'{message.TemplateKey}' için onaylı WhatsApp şablonu yok.", permanent: true, ct);
            return;
        }

        var resolved = await providerFactory.ResolveAsync<IWhatsAppProvider>(tenantId, ct);
        message.ProviderKey = resolved.ProviderKey;

        var payload = new WaTemplateMessage(
            PhoneNumbers.ToProviderFormat(message.ToAddress),
            waTemplate.TemplateName,
            waTemplate.Language,
            BuildBodyParams(waTemplate.ParamMapJson, message.ParamsJson));

        WaSendResult result;
        try
        {
            result = await resolved.Instance.SendTemplateAsync(payload, ct);
        }
        catch (Exception ex) when (ex is WhatsAppProviderException or HttpRequestException or TaskCanceledException)
        {
            await FailAsync(message, ex.Message, permanent: false, ct);
            return;
        }

        if (!result.Success)
        {
            await FailAsync(message, result.Error, permanent: true, ct);
            return;
        }

        Succeed(message, result.ProviderMessageId, creditCost: null);
    }

    /// <summary>ParamMapJson sıralı yer tutucu adları → mesajın ParamsJson değerleri ({{1}}, {{2}}...).</summary>
    internal static IReadOnlyList<string> BuildBodyParams(string? paramMapJson, string? paramsJson)
    {
        if (string.IsNullOrWhiteSpace(paramMapJson)) return [];

        string[]? keys;
        Dictionary<string, string>? values;
        try
        {
            keys = JsonSerializer.Deserialize<string[]>(paramMapJson);
            values = string.IsNullOrWhiteSpace(paramsJson)
                ? []
                : JsonSerializer.Deserialize<Dictionary<string, string>>(paramsJson);
        }
        catch (JsonException)
        {
            return [];
        }

        if (keys is null) return [];
        values ??= [];
        return [.. keys.Select(k => values.TryGetValue(k, out var v) ? v : "")];
    }

    private void Succeed(OutboundMessage message, string? providerMessageId, decimal? creditCost)
    {
        message.State = OutboundMessageState.Sent;
        message.ProviderMessageId = providerMessageId;
        message.CreditCost = creditCost;
        message.SentAtUtc = clock.UtcNow;
        message.Error = null;
        message.NextAttemptAtUtc = null;

        logger.LogInformation("Mesaj gönderildi. Id={Id} Kanal={Channel} Sürücü={Provider} SağlayıcıId={ProviderId}",
            message.Id, message.Channel, message.ProviderKey, providerMessageId);
    }

    /// <summary>
    /// Hata durum makinesi. Kalıcı hata ya da deneme hakkı bitmesi → Failed (+ kanal politikası
    /// izin veriyorsa SMS fallback); geçici hata → Pending + artan aralıkla NextAttemptAtUtc.
    /// </summary>
    private async Task FailAsync(OutboundMessage message, string? error, bool permanent, CancellationToken ct)
    {
        message.Error = Truncate(error);
        message.AttemptCount++;

        if (!permanent && message.AttemptCount < MaxAttempts)
        {
            var backoff = RetryBackoff[Math.Min(message.AttemptCount - 1, RetryBackoff.Length - 1)];
            message.NextAttemptAtUtc = clock.UtcNow.Add(backoff);
            message.State = OutboundMessageState.Pending;
            logger.LogWarning("Mesaj gönderimi başarısız (geçici). Id={Id} Deneme={Attempt} Sonraki={Next}",
                message.Id, message.AttemptCount, message.NextAttemptAtUtc);
            return;
        }

        message.NextAttemptAtUtc = null;
        message.State = OutboundMessageState.Failed;
        logger.LogWarning("Mesaj gönderimi başarısız (kalıcı). Id={Id} Deneme={Attempt} Hata={Error}",
            message.Id, message.AttemptCount, error);

        await TryCreateSmsFallbackAsync(message, ct);
    }

    /// <summary>WhatsApp başarısızlığında politika izin veriyorsa aynı içeriği SMS olarak kuyruğa alır.</summary>
    private async Task TryCreateSmsFallbackAsync(OutboundMessage message, CancellationToken ct)
    {
        if (message.Channel != MessageChannel.WhatsApp) return;
        if (message.FallbackOfMessageId is not null) return; // zincir tek adımdır

        var policy = await db.AutomationRules.AsNoTracking()
            .Where(r => r.TemplateKey == message.TemplateKey)
            .Select(r => (ChannelPolicy?)r.ChannelPolicy)
            .FirstOrDefaultAsync(ct) ?? ChannelPolicy.WhatsAppFirstThenSms;

        if (policy != ChannelPolicy.WhatsAppFirstThenSms) return;

        var smsBody = await db.MessageTemplates.AsNoTracking()
            .Where(t => t.TemplateKey == message.TemplateKey && t.Channel == MessageChannel.Sms && t.IsActive)
            .Select(t => t.Body)
            .FirstOrDefaultAsync(ct);

        var parameters = string.IsNullOrWhiteSpace(message.ParamsJson)
            ? []
            : JsonSerializer.Deserialize<Dictionary<string, string>>(message.ParamsJson) ?? [];

        db.OutboundMessages.Add(new OutboundMessage
        {
            PatientId = message.PatientId,
            Channel = MessageChannel.Sms,
            Kind = message.Kind,
            TemplateKey = message.TemplateKey,
            RenderedBody = smsBody is null ? message.RenderedBody : MessageRenderer.Render(smsBody, parameters),
            ParamsJson = message.ParamsJson,
            ToAddress = message.ToAddress,
            ScheduledAtUtc = clock.UtcNow,
            RefType = message.RefType,
            RefId = message.RefId,
            FallbackOfMessageId = message.Id,
            CorrelationId = message.CorrelationId,
            State = OutboundMessageState.Pending,
        });

        logger.LogInformation("WhatsApp başarısız; SMS fallback kuyruğa alındı. Kaynak={Id}", message.Id);
    }

    /// <summary>SMS gönderici başlığı: hastanın kliniği (yoksa kiracı adı) — Netgsm onaylı msgheader.</summary>
    private async Task<string> ClinicHeaderAsync(OutboundMessage message, CancellationToken ct)
    {
        if (message.PatientId is { } patientId)
        {
            var name = await (from p in db.Patients.AsNoTracking()
                              join c in db.Clinics.AsNoTracking() on p.ClinicId equals c.Id
                              where p.Id == patientId
                              select c.Name).FirstOrDefaultAsync(ct);
            if (name is not null) return name;
        }

        return await db.Clinics.AsNoTracking().OrderBy(c => c.Id).Select(c => c.Name).FirstOrDefaultAsync(ct)
            ?? "Klinik";
    }

    private static string? Truncate(string? value) =>
        value is null ? null : value.Length <= 1000 ? value : value[..1000];
}
