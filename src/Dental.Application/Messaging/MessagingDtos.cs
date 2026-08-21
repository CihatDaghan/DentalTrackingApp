using Dental.Domain.Enums;

namespace Dental.Application.Messaging;

/// <summary>Kiracı şablonlarında kullanılan yer tutucu adları (süslü parantezle yazılır).</summary>
public static class MessagePlaceholders
{
    public const string PatientName = "hasta_adi";
    public const string AppointmentDate = "randevu_tarihi";
    public const string AppointmentTime = "randevu_saati";
    public const string ClinicName = "klinik_adi";
    public const string Balance = "bakiye";
    public const string PaymentLink = "odeme_linki";
    public const string ConsentLink = "onam_linki";
    public const string DoctorName = "hekim_adi";
}

/// <summary>Standart şablon anahtarları; seed bu listeden üretilir.</summary>
public static class MessageTemplateKeys
{
    public const string AppointmentReminder = "appointment_reminder";
    public const string Birthday = "birthday";
    public const string PaymentReminder = "payment_reminder";
    public const string Recall = "recall";
    public const string ConsentLink = "consent_link";
    public const string PaymentLink = "payment_link";
    public const string Bulk = "bulk";

    public static readonly IReadOnlyList<string> All =
        [AppointmentReminder, Birthday, PaymentReminder, Recall, ConsentLink, PaymentLink, Bulk];
}

// ---- Outbox ----

/// <summary>
/// Tek mesaj kuyruğa alma isteği (iç servis sözleşmesi).
/// </summary>
/// <param name="Channel">NULL ise kanal politikasından çözülür (varsayılan WhatsApp→SMS).</param>
/// <param name="Params">Yer tutucu adı → değer; şablonda geçmeyen anahtarlar yok sayılır.</param>
/// <param name="ToAddressOverride">Hasta kartındaki telefon yerine kullanılacak alıcı (test/kurum).</param>
public sealed record MessageEnqueueRequest(
    string TemplateKey,
    long? PatientId = null,
    MessageChannel? Channel = null,
    MessageKind Kind = MessageKind.Transactional,
    IReadOnlyDictionary<string, string>? Params = null,
    DateTime? ScheduledAtUtc = null,
    string? RefType = null,
    long? RefId = null,
    string? ToAddressOverride = null,
    string? BodyOverride = null,
    long? FallbackOfMessageId = null,
    string? Locale = null);

public sealed record OutboundMessageDto(
    long Id,
    long? PatientId,
    string? PatientName,
    MessageChannel Channel,
    MessageKind Kind,
    string TemplateKey,
    string RenderedBody,
    string ToAddress,
    OutboundMessageState State,
    MessageSkipReason? SkipReason,
    string? ProviderKey,
    string? ProviderMessageId,
    DateTime ScheduledAtUtc,
    DateTime? SentAtUtc,
    DateTime? DeliveredAtUtc,
    string? Error,
    int AttemptCount,
    DateTime? NextAttemptAtUtc,
    long? FallbackOfMessageId,
    string? RefType,
    long? RefId,
    decimal? CreditCost,
    string CorrelationId,
    DateTime CreatedAtUtc);

public sealed record MessageListQuery(
    MessageChannel? Channel = null,
    OutboundMessageState? State = null,
    long? PatientId = null,
    DateOnly? From = null,
    DateOnly? To = null,
    int Page = 1,
    int PageSize = 25);

/// <summary>API'den tekil gönderim: şablonlu ya da serbest metinli (BodyOverride).</summary>
public sealed record MessageSendRequest(
    long PatientId,
    string TemplateKey = MessageTemplateKeys.Bulk,
    MessageChannel? Channel = null,
    MessageKind Kind = MessageKind.Transactional,
    string? BodyOverride = null,
    Dictionary<string, string>? Params = null,
    DateTime? ScheduledAtUtc = null);

/// <summary>
/// Toplu gönderim hasta filtresi. Tüm alanlar opsiyoneldir; verilenler VE ile birleşir.
/// </summary>
/// <param name="LastVisitFrom">Son randevusu bu tarihten sonra olan hastalar.</param>
/// <param name="HasDebt">true: bakiyesi &gt; 0 olanlar, false: borcu olmayanlar.</param>
/// <param name="BirthMonth">Doğum ayı (1-12) — doğum günü kampanyaları için.</param>
public sealed record BulkAudienceFilter(
    DateOnly? LastVisitFrom = null,
    DateOnly? LastVisitTo = null,
    long? DoctorUserId = null,
    bool? HasDebt = null,
    int? BirthMonth = null,
    long? TagId = null);

public sealed record BulkMessageRequest(
    string TemplateKey,
    BulkAudienceFilter Filter,
    MessageChannel? Channel = null,
    MessageKind Kind = MessageKind.Commercial,
    string? BodyOverride = null,
    DateTime? ScheduledAtUtc = null);

/// <param name="Targeted">Filtreyle eşleşen hasta sayısı.</param>
/// <param name="Enqueued">Kuyruğa giren mesaj sayısı.</param>
/// <param name="SkippedNoConsent">İzni olmadığı için atlanan hasta sayısı (İYS/KVKK).</param>
/// <param name="SkippedNoPhone">Geçerli numarası olmadığı için atlanan hasta sayısı.</param>
public sealed record BulkMessageResult(
    int Targeted,
    int Enqueued,
    int SkippedNoConsent,
    int SkippedNoPhone,
    IReadOnlyList<long> MessageIds);

// ---- Şablonlar ----

public sealed record MessageTemplateDto(
    long Id,
    string TemplateKey,
    MessageChannel Channel,
    string Locale,
    string Body,
    MessageKind Kind,
    bool IsActive);

public sealed record MessageTemplateUpsertRequest(
    string TemplateKey,
    MessageChannel Channel,
    string Locale,
    string Body,
    MessageKind Kind = MessageKind.Transactional,
    bool IsActive = true);

public sealed record WhatsAppTemplateDto(
    long Id,
    string TemplateName,
    string Language,
    string Category,
    string BodySpec,
    string? ParamMapJson,
    WaTemplateStatus MetaStatus,
    DateTime? MetaUpdatedAtUtc,
    string? TemplateKey);

public sealed record WhatsAppTemplateUpsertRequest(
    string TemplateName,
    string Language,
    string Category,
    string BodySpec,
    string? ParamMapJson = null,
    WaTemplateStatus MetaStatus = WaTemplateStatus.Draft,
    string? TemplateKey = null);

// ---- Otomasyon kuralları ----

public sealed record AutomationRuleDto(
    long Id,
    AutomationRuleType RuleType,
    bool IsEnabled,
    int OffsetHours,
    ChannelPolicy ChannelPolicy,
    string TemplateKey,
    TimeOnly? SendAtLocalTime);

public sealed record AutomationRuleUpsertRequest(
    AutomationRuleType RuleType,
    bool IsEnabled,
    int OffsetHours = 24,
    ChannelPolicy ChannelPolicy = ChannelPolicy.WhatsAppFirstThenSms,
    string? TemplateKey = null,
    TimeOnly? SendAtLocalTime = null);
