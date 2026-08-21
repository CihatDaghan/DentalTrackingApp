namespace Dental.Application.Abstractions;

/// <summary>
/// İYS/KVKK ayrımı: Transactional (randevu/ödeme/kontrol hatırlatma) İYS onayı gerektirmez;
/// Commercial (kampanya) gönderim öncesi consent kontrolü ve sağlayıcı tarafında İYS filtresi zorunludur.
/// </summary>
public enum SmsKind
{
    Transactional = 0,
    Commercial = 1
}

/// <param name="To">Alıcı numara (örn. 905XXXXXXXXX).</param>
/// <param name="Header">Onaylı gönderici başlığı (msgheader).</param>
/// <param name="ClientRef">Bizim tarafın korelasyon anahtarı (OutboundMessages.CorrelationId).</param>
public sealed record SmsMessage(
    string To,
    string Body,
    string Header,
    SmsKind Kind = SmsKind.Transactional,
    string? ClientRef = null);

public sealed record SmsSendResult(
    bool Success,
    string? ProviderMessageId = null,
    string? Error = null,
    decimal? CreditCost = null);

/// <summary>
/// SMS gönderim portu. Sürücüler geçici hatalarda retry YAPMAZ; retry/outbox üst katmanın işidir.
/// Sağlayıcının iş reddi (hatalı başlık, yetersiz kredi vb.) <see cref="SmsSendResult.Error"/> ile döner;
/// taşıma/protokol hatası <see cref="SmsProviderException"/> fırlatır.
/// </summary>
public interface ISmsProvider
{
    Task<SmsSendResult> SendAsync(SmsMessage message, CancellationToken ct = default);
    Task<decimal> GetBalanceAsync(CancellationToken ct = default);
}

public sealed class SmsProviderException : Exception
{
    public SmsProviderException(string message) : base(message) { }
    public SmsProviderException(string message, Exception innerException) : base(message, innerException) { }
}
