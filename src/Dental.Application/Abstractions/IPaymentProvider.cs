namespace Dental.Application.Abstractions;

/// <param name="ConversationId">PaymentIntents.Id; sağlayıcıya gönderilir, callback'te eşleşme anahtarıdır.</param>
/// <param name="CallbackUrl">Ödeme sonrası sağlayıcının POST edeceği bizim uç (token taşır).</param>
public sealed record PaymentCheckoutRequest(
    string ConversationId,
    decimal Amount,
    string Currency,
    string Description,
    string BuyerName,
    string? BuyerEmail,
    string? BuyerPhone,
    string CallbackUrl);

/// <param name="ProviderToken">Sağlayıcının oturum/checkout token'ı; doğrulamada kullanılır.</param>
/// <param name="PaymentPageUrl">Hastaya SMS/WA/e-posta ile gönderilecek ödeme sayfası bağlantısı.</param>
public sealed record PaymentCheckoutResult(
    string ProviderToken,
    string PaymentPageUrl);

public enum PaymentVerifyStatus
{
    Pending = 0,
    Success = 1,
    Failure = 2
}

public sealed record PaymentVerifyResult(
    PaymentVerifyStatus Status,
    string? ProviderPaymentId = null,
    decimal? PaidAmount = null,
    string? RawJson = null,
    string? Error = null);

/// <summary>
/// Sanal POS / ödeme linki portu. Callback verisine ASLA tek başına güvenilmez:
/// callback geldiğinde <see cref="VerifyPaymentAsync"/> ile sunucudan yeniden doğrulanır.
/// Sürücüler retry yapmaz; retry/outbox üst katmanın işidir.
/// </summary>
public interface IPaymentProvider
{
    Task<PaymentCheckoutResult> CreateCheckoutAsync(PaymentCheckoutRequest request, CancellationToken ct = default);
    Task<PaymentVerifyResult> VerifyPaymentAsync(string providerToken, CancellationToken ct = default);
}

public sealed class PaymentProviderException : Exception
{
    public PaymentProviderException(string message) : base(message) { }
    public PaymentProviderException(string message, Exception innerException) : base(message, innerException) { }
}
