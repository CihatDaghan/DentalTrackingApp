using Dental.Application.Common;
using Dental.Domain.Enums;

namespace Dental.Application.Messaging;

/// <summary>
/// Giden mesaj kuyruğu. TÜM gönderimler buradan geçer (doğrudan sürücü çağrısı yasak):
/// şablon çözümü + yer tutucu doldurma + ticari mesajda izin kontrolü + numara normalizasyonu
/// tek yerde uygulanır. Kayıt Pending yazılır, gerçek gönderimi <see cref="IMessageDispatcher"/> yapar.
/// </summary>
public interface IMessageOutboxService
{
    /// <summary>
    /// Tek mesajı kuyruğa alır. İzin/numara engeli varsa kayıt yine oluşur ama
    /// State=Skipped olur (gönderilmeyenler de raporlanabilsin diye).
    /// </summary>
    Task<OutboundMessageDto> EnqueueAsync(MessageEnqueueRequest request, CancellationToken ct = default);

    /// <summary>Hasta filtresine uyan herkese kuyruğa alır; izinsiz/numarasız olanlar sayılarak raporlanır.</summary>
    Task<BulkMessageResult> EnqueueBulkAsync(BulkMessageRequest request, CancellationToken ct = default);

    Task<PagedResult<OutboundMessageDto>> ListAsync(MessageListQuery query, CancellationToken ct = default);

    Task<OutboundMessageDto> GetAsync(long id, CancellationToken ct = default);
}

/// <summary>
/// Outbox gönderim motoru (EDocumentDispatcher kalıbı). Çağıran her zaman tenant bağlamı
/// kurulmuş bir scope'ta olmalıdır (job'larda ITenantScopeFactory).
/// </summary>
public interface IMessageDispatcher
{
    /// <summary>Tek mesajı kanalına göre sürücüye verir; yeni durumu döner.</summary>
    Task<OutboundMessageState> DispatchAsync(long messageId, CancellationToken ct = default);

    /// <summary>Zamanı gelmiş Pending mesajları işler; işlenen mesaj sayısını döner.</summary>
    Task<int> DispatchPendingAsync(int batchSize = 100, CancellationToken ct = default);

    /// <summary>WhatsApp webhook'undan gelen teslim/okundu durumunu outbox'a işler.</summary>
    Task<bool> ApplyDeliveryStatusAsync(
        string providerMessageId, string status, DateTime atUtc, string? error, CancellationToken ct = default);
}

public interface IMessageTemplateService
{
    Task<IReadOnlyList<MessageTemplateDto>> ListAsync(bool includeInactive = false, CancellationToken ct = default);
    Task<MessageTemplateDto> GetAsync(long id, CancellationToken ct = default);
    Task<MessageTemplateDto> CreateAsync(MessageTemplateUpsertRequest request, CancellationToken ct = default);
    Task<MessageTemplateDto> UpdateAsync(long id, MessageTemplateUpsertRequest request, CancellationToken ct = default);
    Task DeleteAsync(long id, CancellationToken ct = default);

    Task<IReadOnlyList<WhatsAppTemplateDto>> ListWhatsAppAsync(CancellationToken ct = default);
    Task<WhatsAppTemplateDto> GetWhatsAppAsync(long id, CancellationToken ct = default);
    Task<WhatsAppTemplateDto> CreateWhatsAppAsync(WhatsAppTemplateUpsertRequest request, CancellationToken ct = default);
    Task<WhatsAppTemplateDto> UpdateWhatsAppAsync(long id, WhatsAppTemplateUpsertRequest request, CancellationToken ct = default);
    Task DeleteWhatsAppAsync(long id, CancellationToken ct = default);
}

public interface IAutomationRuleService
{
    Task<IReadOnlyList<AutomationRuleDto>> ListAsync(CancellationToken ct = default);
    Task<AutomationRuleDto> GetAsync(long id, CancellationToken ct = default);
    /// <summary>Kural türü kiracıda tekildir: aynı tür varsa günceller, yoksa oluşturur.</summary>
    Task<AutomationRuleDto> UpsertAsync(AutomationRuleUpsertRequest request, CancellationToken ct = default);
    Task<AutomationRuleDto> UpdateAsync(long id, AutomationRuleUpsertRequest request, CancellationToken ct = default);
    Task DeleteAsync(long id, CancellationToken ct = default);
    /// <summary>Eksik varsayılan kuralları kiracıya ekler (idempotent); eklenen sayıyı döner.</summary>
    Task<int> EnsureDefaultsAsync(CancellationToken ct = default);
}

/// <summary>
/// Otomasyon iş mantığı (job gövdeleri). Zaman hesapları TrTime ile yerel (TR) gün/saate göre yapılır.
/// Her metot tenant bağlamı kurulmuş bir scope'ta çağrılır ve kuyruğa alınan mesaj sayısını döner.
/// </summary>
public interface IMessageAutomationService
{
    Task<int> QueueAppointmentRemindersAsync(CancellationToken ct = default);
    Task<int> QueueBirthdayGreetingsAsync(CancellationToken ct = default);
    Task<int> QueuePaymentOverdueRemindersAsync(CancellationToken ct = default);
    Task<int> QueueRecallRemindersAsync(CancellationToken ct = default);
}
