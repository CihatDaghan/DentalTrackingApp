using System.Collections.Concurrent;
using System.Text.Json;
using Dental.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Dental.Integrations.Payments.Fake;

/// <summary>
/// Geliştirme/E2E sürücüsü: sağlayıcı yerine yerel bir "test ödeme sayfası" URL'i üretir,
/// durumunu bellekte tutar. VerifyPaymentAsync ilk çağrıdan itibaren deterministik olarak
/// Success döner; E2E testleri sağlayıcısız koşar. Singleton kaydedilmelidir (bellek içi durum).
/// </summary>
public sealed class FakePaymentProvider(ILogger<FakePaymentProvider> logger, string paymentPageBaseUrl = "http://localhost:4200/dev/fake-payment") : IPaymentProvider
{
    private readonly ConcurrentDictionary<string, PaymentCheckoutRequest> _checkouts = new();

    public Task<PaymentCheckoutResult> CreateCheckoutAsync(PaymentCheckoutRequest request, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        var token = $"fake-tok-{request.ConversationId}";
        _checkouts[token] = request;
        var url = $"{paymentPageBaseUrl.TrimEnd('/')}?token={Uri.EscapeDataString(token)}&callback={Uri.EscapeDataString(request.CallbackUrl)}";
        logger.LogInformation("FAKE ödeme linki üretildi. ConversationId={ConversationId} Amount={Amount} {Currency} Url={Url}",
            request.ConversationId, request.Amount, request.Currency, url);
        return Task.FromResult(new PaymentCheckoutResult(token, url));
    }

    public Task<PaymentVerifyResult> VerifyPaymentAsync(string providerToken, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (!_checkouts.TryGetValue(providerToken, out var request))
        {
            return Task.FromResult(new PaymentVerifyResult(
                PaymentVerifyStatus.Failure,
                Error: $"Bilinmeyen ödeme token'ı: {providerToken}"));
        }

        var paymentId = $"fake-pay-{request.ConversationId}";
        var raw = JsonSerializer.Serialize(new
        {
            token = providerToken,
            conversationId = request.ConversationId,
            paidAmount = request.Amount,
            currency = request.Currency,
            status = "SUCCESS",
        });
        return Task.FromResult(new PaymentVerifyResult(
            PaymentVerifyStatus.Success,
            ProviderPaymentId: paymentId,
            PaidAmount: request.Amount,
            RawJson: raw));
    }
}
