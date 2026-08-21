using Dental.Application.Abstractions;
using Dental.Application.Payments;
using Dental.Infrastructure.Consents;
using Dental.Infrastructure.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Dental.Api.Controllers;

/// <summary>
/// Dış sağlayıcı webhook/callback uçları. Auth'suzdur; güvenlik kimlik doğrulamayla değil
/// (a) token bilgisi ve (b) imza doğrulamasıyla sağlanır. IP bazlı "public" hız sınırı uygulanır.
/// </summary>
[ApiController]
[Route("api/webhooks")]
[AllowAnonymous]
[EnableRateLimiting("public")]
public sealed class WebhooksController(
    IPublicPaymentService publicPayments,
    IWhatsAppWebhookService whatsAppWebhook,
    IOptions<PublicOptions> publicOptions,
    IOptions<WhatsAppWebhookOptions> whatsAppOptions,
    ILogger<WebhooksController> logger) : ControllerBase
{
    /// <summary>
    /// iyzico ödeme dönüşü. Hasta tarayıcısı buraya POST eder; callback verisine güvenilmez,
    /// sonuç sunucudan yeniden doğrulanır (PaymentLinkService.HandleCallbackAsync).
    /// İşlem bitince hasta kendi ödeme sayfamıza geri yönlendirilir (POST-redirect-GET).
    /// </summary>
    [HttpPost("iyzico")]
    public async Task<IActionResult> Iyzico(
        [FromQuery] Guid? intent, [FromForm] string? token, CancellationToken ct)
    {
        if (intent is null && string.IsNullOrWhiteSpace(token))
            return BadRequest(new { error = "Ödeme referansı yok." });

        var result = await publicPayments.HandleCallbackAsync(intent, token, ct);
        logger.LogInformation("iyzico callback işlendi. IntentId={Id} Durum={Status} Mükerrer={Duplicate}",
            result.IntentId, result.Status, result.AlreadyProcessed);

        var target = $"{publicOptions.Value.BaseUrl.TrimEnd('/')}/p/payment/{result.PublicToken:D}";
        return Redirect(target);
    }

    /// <summary>Meta webhook doğrulaması: hub.verify_token eşleşirse hub.challenge düz metin döner.</summary>
    [HttpGet("whatsapp")]
    public IActionResult WhatsAppVerify(
        [FromQuery(Name = "hub.mode")] string? mode,
        [FromQuery(Name = "hub.verify_token")] string? verifyToken,
        [FromQuery(Name = "hub.challenge")] string? challenge)
    {
        var expected = whatsAppOptions.Value.VerifyToken;
        if (mode != "subscribe" || string.IsNullOrEmpty(expected) || verifyToken != expected)
        {
            logger.LogWarning("WhatsApp webhook doğrulaması reddedildi. Mode={Mode}", mode);
            return StatusCode(StatusCodes.Status403Forbidden);
        }

        return Content(challenge ?? "", "text/plain");
    }

    /// <summary>
    /// Meta webhook olayları. Gövde HAM baytlarıyla X-Hub-Signature-256 (app secret ile HMAC-SHA256)
    /// doğrulanır; imza geçersizse 401 döner ve gövde HİÇ işlenmez.
    /// </summary>
    [HttpPost("whatsapp")]
    public async Task<IActionResult> WhatsAppEvents(CancellationToken ct)
    {
        using var buffer = new MemoryStream();
        await Request.Body.CopyToAsync(buffer, ct);
        var payload = buffer.ToArray();

        var appSecret = whatsAppOptions.Value.AppSecret;
        if (string.IsNullOrEmpty(appSecret))
        {
            logger.LogError("WhatsApp webhook app secret yapılandırılmamış; olay reddedildi.");
            return Unauthorized(new { error = "Webhook imza anahtarı yapılandırılmamış." });
        }

        var signature = Request.Headers["X-Hub-Signature-256"].ToString();
        if (!WaWebhookParser.VerifySignature(payload, signature, appSecret))
        {
            logger.LogWarning("WhatsApp webhook imzası geçersiz.");
            return Unauthorized(new { error = "İmza doğrulanamadı." });
        }

        WaWebhookEvent parsed;
        try
        {
            parsed = WaWebhookParser.Parse(System.Text.Encoding.UTF8.GetString(payload));
        }
        catch (WhatsAppProviderException ex)
        {
            logger.LogWarning(ex, "WhatsApp webhook gövdesi çözümlenemedi.");
            return BadRequest(new { error = ex.Message });
        }

        var applied = await whatsAppWebhook.HandleAsync(parsed, ct);
        return Ok(new { applied, statuses = parsed.StatusUpdates.Count, messages = parsed.IncomingMessages.Count });
    }
}
