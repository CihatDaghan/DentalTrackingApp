using Dental.Application.Abstractions;
using Dental.Integrations.Sms.Fake;
using Microsoft.Extensions.Logging;

namespace Dental.Integrations.WhatsApp.Fake;

/// <summary>
/// Geliştirme varsayılanı: şablon mesajını loglar, her zaman başarılı döner.
/// ProviderMessageId deterministiktir (E2E doğrulaması için).
/// SMS sürücüsüyle aynı test numaraları geçerlidir (bkz. <see cref="FakeSmsProvider"/>):
/// fallback zincirinin WhatsApp ayağını sürücüsüz test etmek içindir.
/// </summary>
public sealed class FakeWhatsAppProvider(ILogger<FakeWhatsAppProvider> logger) : IWhatsAppProvider
{
    public Task<WaSendResult> SendTemplateAsync(WaTemplateMessage message, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (message.To == FakeSmsProvider.TransportFailureNumber)
            throw new WhatsAppProviderException("FAKE WhatsApp: taşıma hatası (test numarası).");
        if (message.To == FakeSmsProvider.RejectNumber)
            return Task.FromResult(new WaSendResult(Success: false, Error: "FAKE WhatsApp: sağlayıcı reddetti (test numarası)."));

        var id = FakeSmsProvider.DeterministicId("fake-wa", message.To, message.TemplateName, string.Join(",", message.BodyParams));
        logger.LogInformation(
            "FAKE WhatsApp şablon mesajı gönderildi. To={To} Template={Template} Lang={Lang} Params={Params} Id={Id}",
            message.To, message.TemplateName, message.Language, string.Join(" | ", message.BodyParams), id);
        return Task.FromResult(new WaSendResult(Success: true, ProviderMessageId: id));
    }
}
