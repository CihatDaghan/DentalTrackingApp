namespace Dental.Domain.Enums;

/// <summary>Giden mesajın taşındığı kanal. E-posta altyapısı G'de yer tutucudur (sürücü yok).</summary>
public enum MessageChannel : byte
{
    Sms = 1,
    WhatsApp = 2,
    Email = 3,
}

/// <summary>
/// İYS/KVKK ayrımı: Transactional (randevu/ödeme/kontrol/onam) izin gerektirmez;
/// Commercial (kampanya, doğum günü) gönderim öncesi CommunicationConsent kontrolüne tabidir.
/// </summary>
public enum MessageKind : byte
{
    Transactional = 1,
    Commercial = 2,
}

/// <summary>
/// Outbox durum makinesi. Pending → Sending → Sent → Delivered (webhook);
/// hata → Failed (yeniden deneme bitince), izin/numara engeli → Skipped (hiç denenmez).
/// </summary>
public enum OutboundMessageState : byte
{
    Pending = 1,
    Sending = 2,
    Sent = 3,
    Delivered = 4,
    Failed = 5,
    Skipped = 6,
}

/// <summary>Gönderilmeme gerekçesi; Skipped durumundaki mesajlarda dolu olur.</summary>
public enum MessageSkipReason : byte
{
    /// <summary>Ticari mesaj, hastanın ilgili kanal için izni yok (İYS/KVKK).</summary>
    NoConsent = 1,
    /// <summary>Telefon numarası yok ya da TR formatına normalize edilemedi.</summary>
    InvalidNumber = 2,
    /// <summary>Kiracıda bu şablon anahtarı için etkin şablon bulunamadı.</summary>
    NoTemplate = 3,
    /// <summary>Kanal kiracı ayarında kapalı ya da sürücüsü yok.</summary>
    ChannelDisabled = 4,
    /// <summary>Mükerrer gönderim koruması (aynı referans için yakın zamanda mesaj var).</summary>
    Duplicate = 5,
}

/// <summary>Meta tarafındaki şablon onay durumu; yalnız Approved şablonla WhatsApp gönderimi yapılır.</summary>
public enum WaTemplateStatus : byte
{
    Draft = 1,
    Submitted = 2,
    Approved = 3,
    Rejected = 4,
}

/// <summary>Otomasyon kuralı türü; kiracı başına her türden en fazla bir kural olur (UQ).</summary>
public enum AutomationRuleType : byte
{
    AppointmentReminder = 1,
    Birthday = 2,
    PaymentOverdue = 3,
    Recall = 4,
}

/// <summary>
/// Kanal politikası: WhatsApp önce denenip başarısız olursa (onaysız şablon, sürücü hatası)
/// aynı içerik SMS'e düşer; SmsOnly/WhatsAppOnly fallback üretmez.
/// </summary>
public enum ChannelPolicy : byte
{
    WhatsAppFirstThenSms = 1,
    SmsOnly = 2,
    WhatsAppOnly = 3,
}

/// <summary>
/// Ödeme linki yaşam döngüsü. Paid'e yalnız sunucudan yeniden doğrulama (VerifyPaymentAsync)
/// sonrası geçilir; callback verisine tek başına güvenilmez.
/// </summary>
public enum PaymentIntentStatus : byte
{
    Created = 1,
    LinkSent = 2,
    Paid = 3,
    Failed = 4,
    Expired = 5,
    Refunded = 6,
}
