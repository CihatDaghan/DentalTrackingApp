using System.Security.Cryptography;
using System.Text;
using Dental.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Dental.Integrations.Sms.Fake;

/// <summary>
/// Geliştirme varsayılanı (TR sağlayıcılarında sandbox yok): mesajı loglar, her zaman başarılı döner.
/// ProviderMessageId deterministiktir; aynı mesaj aynı kimliği üretir (E2E doğrulaması için).
///
/// TEST NUMARALARI (yalnız bu sürücüde; 599 öneki TR'de tahsis edilmemiştir):
/// 905990000000 → sağlayıcı iş reddi (Success=false, yeniden denenmez),
/// 905980000000 → taşıma hatası (exception, artan aralıkla yeniden denenir).
/// Outbox'ın kalıcı/geçici hata yollarını sürücüsüz test edebilmek içindir.
/// </summary>
public sealed class FakeSmsProvider(ILogger<FakeSmsProvider> logger) : ISmsProvider
{
    public const string RejectNumber = "905990000000";
    public const string TransportFailureNumber = "905980000000";

    public Task<SmsSendResult> SendAsync(SmsMessage message, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (message.To == TransportFailureNumber)
            throw new SmsProviderException("FAKE SMS: taşıma hatası (test numarası).");
        if (message.To == RejectNumber)
            return Task.FromResult(new SmsSendResult(Success: false, Error: "FAKE SMS: sağlayıcı reddetti (test numarası)."));

        var id = DeterministicId("fake-sms", message.To, message.Body);
        logger.LogInformation(
            "FAKE SMS gönderildi. To={To} Header={Header} Kind={Kind} ClientRef={ClientRef} Id={Id} Body={Body}",
            message.To, message.Header, message.Kind, message.ClientRef, id, message.Body);
        return Task.FromResult(new SmsSendResult(Success: true, ProviderMessageId: id));
    }

    public Task<decimal> GetBalanceAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(1000m);
    }

    internal static string DeterministicId(string prefix, params string[] parts)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("|", parts)));
        return $"{prefix}-{Convert.ToHexStringLower(hash)[..12]}";
    }
}
