using Dental.Domain.Common;
using Dental.Domain.Enums;

namespace Dental.Domain.Entities;

/// <summary>
/// Kanal-bağımsız giden mesaj kuyruğu (SMS + WhatsApp + e-posta ortak outbox'ı).
/// Gönderim HER ZAMAN buradan geçer: servisler doğrudan sürücü çağırmaz, kaydı Pending yazar;
/// <c>MessageDispatchJob</c> kuyruğu işler. Böylece izin filtresi, yeniden deneme ve
/// SMS fallback tek yerde toplanır ve "gönderilenler" ekranı tam kayda sahip olur.
/// </summary>
public class OutboundMessage : TenantEntity
{
    public long? PatientId { get; set; }
    public MessageChannel Channel { get; set; }
    public MessageKind Kind { get; set; } = MessageKind.Transactional;
    /// <summary>MessageTemplate.TemplateKey (appointment_reminder, birthday, payment_link...).</summary>
    public required string TemplateKey { get; set; }
    /// <summary>Yer tutucuları doldurulmuş nihai metin; şablon sonradan değişse de gönderilen metin sabittir.</summary>
    public required string RenderedBody { get; set; }
    /// <summary>
    /// Yer tutucu adı → değer eşlemesi (JSON). WhatsApp şablon gönderiminde Meta'nın
    /// sıralı {{1}},{{2}}... parametreleri bu sözlükten ParamMapJson sırasıyla üretilir.
    /// </summary>
    public string? ParamsJson { get; set; }
    /// <summary>E.164 normalize alıcı (+905XXXXXXXXX) ya da e-posta adresi.</summary>
    public required string ToAddress { get; set; }
    public OutboundMessageState State { get; set; } = OutboundMessageState.Pending;
    public MessageSkipReason? SkipReason { get; set; }
    /// <summary>Gönderimi yapan sürücü anahtarı ('netgsm' | 'meta' | 'fake').</summary>
    public string? ProviderKey { get; set; }
    public string? ProviderMessageId { get; set; }
    public DateTime ScheduledAtUtc { get; set; }
    public DateTime? SentAtUtc { get; set; }
    public DateTime? DeliveredAtUtc { get; set; }
    public string? Error { get; set; }
    public int AttemptCount { get; set; }
    public DateTime? NextAttemptAtUtc { get; set; }
    /// <summary>Bu mesaj başka bir mesajın (WhatsApp) SMS fallback'i ise kaynağın Id'si.</summary>
    public long? FallbackOfMessageId { get; set; }
    /// <summary>Kaynak kayıt tablosu (Appointment, ConsentForm, PaymentIntent, RecallPlan...).</summary>
    public string? RefType { get; set; }
    public long? RefId { get; set; }
    /// <summary>Sağlayıcının bildirdiği kredi/ücret bilgisi (SMS kontör raporu).</summary>
    public decimal? CreditCost { get; set; }
    /// <summary>Sürücüye ClientRef olarak geçen korelasyon anahtarı; loglarda uçtan uca izlemeyi sağlar.</summary>
    public required string CorrelationId { get; set; }
}

/// <summary>
/// Kiracının metin şablonu. Yer tutucular süslü parantezle yazılır:
/// {hasta_adi} {randevu_tarihi} {randevu_saati} {klinik_adi} {bakiye} {odeme_linki} {onam_linki} {hekim_adi}.
/// (TemplateKey, Channel, Locale) kiracı içinde benzersizdir.
/// </summary>
public class MessageTemplate : TenantEntity
{
    public required string TemplateKey { get; set; }
    public MessageChannel Channel { get; set; } = MessageChannel.Sms;
    public string Locale { get; set; } = "tr";
    public required string Body { get; set; }
    public MessageKind Kind { get; set; } = MessageKind.Transactional;
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// Meta tarafında onaya giden WhatsApp şablonu. Gönderim yalnız <see cref="WaTemplateStatus.Approved"/>
/// şablonla yapılır; onaysızsa mesaj kanal politikasına göre SMS'e düşer.
/// </summary>
public class WhatsAppTemplate : TenantEntity
{
    public required string TemplateName { get; set; }
    public string Language { get; set; } = "tr";
    /// <summary>Meta kategorisi: 'utility' (hatırlatma) | 'marketing' (kampanya).</summary>
    public string Category { get; set; } = "utility";
    /// <summary>Meta'ya sunulan gövde metni ({{1}}, {{2}}... değişkenleriyle).</summary>
    public required string BodySpec { get; set; }
    /// <summary>Sıralı yer tutucu adları (JSON dizi): ["hasta_adi","randevu_tarihi"] → {{1}},{{2}}.</summary>
    public string? ParamMapJson { get; set; }
    public WaTemplateStatus MetaStatus { get; set; } = WaTemplateStatus.Draft;
    public DateTime? MetaUpdatedAtUtc { get; set; }
    /// <summary>Bu şablonun beslediği MessageTemplate anahtarı (appointment_reminder...).</summary>
    public string? TemplateKey { get; set; }
}

/// <summary>
/// Otomatik gönderim kuralı. Kiracı başına her <see cref="AutomationRuleType"/> için tek kayıt;
/// varsayılan olarak yalnız randevu hatırlatma (24 sa önce) açıktır.
/// </summary>
public class AutomationRule : TenantEntity
{
    public AutomationRuleType RuleType { get; set; }
    public bool IsEnabled { get; set; }
    /// <summary>Randevudan kaç saat önce gönderileceği (AppointmentReminder / Recall için).</summary>
    public int OffsetHours { get; set; } = 24;
    public ChannelPolicy ChannelPolicy { get; set; } = ChannelPolicy.WhatsAppFirstThenSms;
    public required string TemplateKey { get; set; }
    /// <summary>Günlük kurallarda (doğum günü, gecikmiş ödeme) yerel gönderim saati.</summary>
    public TimeOnly? SendAtLocalTime { get; set; }
}
