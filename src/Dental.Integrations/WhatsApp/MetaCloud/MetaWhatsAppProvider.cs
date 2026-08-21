using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Dental.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Dental.Integrations.WhatsApp.MetaCloud;

/// <summary>
/// Meta Cloud API şablonlu mesaj sürücüsü. Typed client desenine uygun: HttpClient dışarıdan gelir;
/// retry üst katmandadır, burada retry YOKTUR.
/// </summary>
public sealed class MetaWhatsAppProvider(HttpClient http, MetaWhatsAppSettings settings, ILogger<MetaWhatsAppProvider> logger) : IWhatsAppProvider
{
    public async Task<WaSendResult> SendTemplateAsync(WaTemplateMessage message, CancellationToken ct = default)
    {
        var template = new Dictionary<string, object>
        {
            ["name"] = message.TemplateName,
            ["language"] = new Dictionary<string, string> { ["code"] = message.Language },
        };
        if (message.BodyParams.Count > 0)
        {
            template["components"] = new[]
            {
                new Dictionary<string, object>
                {
                    ["type"] = "body",
                    ["parameters"] = message.BodyParams
                        .Select(p => new Dictionary<string, string> { ["type"] = "text", ["text"] = p })
                        .ToArray(),
                },
            };
        }

        var payload = new Dictionary<string, object>
        {
            ["messaging_product"] = "whatsapp",
            ["recipient_type"] = "individual",
            ["to"] = message.To,
            ["type"] = "template",
            ["template"] = template,
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, BuildMessagesUri())
        {
            Content = JsonContent.Create(payload),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.AccessToken);

        try
        {
            using var response = await http.SendAsync(request, ct).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                var id = TryGetMessageId(body);
                logger.LogInformation("WhatsApp şablon mesajı gönderildi. To={To} Template={Template} Id={Id}",
                    message.To, message.TemplateName, id);
                return new WaSendResult(Success: true, ProviderMessageId: id);
            }

            var error = ParseGraphError(body) ?? $"Meta Graph API HTTP {(int)response.StatusCode}: {Truncate(body)}";
            logger.LogWarning("WhatsApp gönderimi reddedildi. To={To} Template={Template} Error={Error}",
                message.To, message.TemplateName, error);
            return new WaSendResult(Success: false, Error: error);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException ex)
        {
            throw new WhatsAppProviderException("Meta Graph API isteği zaman aşımına uğradı.", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new WhatsAppProviderException($"Meta Graph API isteği başarısız: {ex.Message}", ex);
        }
    }

    private Uri BuildMessagesUri()
    {
        if (!string.IsNullOrWhiteSpace(settings.GraphApiBase))
            return new Uri($"{settings.GraphApiBase.TrimEnd('/')}/{settings.PhoneNumberId}/messages");
        return http.BaseAddress is not null
            ? new Uri(http.BaseAddress, $"{settings.PhoneNumberId}/messages")
            : throw new WhatsAppProviderException("Meta GraphApiBase ayarı eksik.");
    }

    private static string? TryGetMessageId(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("messages", out var messages) &&
                messages.ValueKind == JsonValueKind.Array &&
                messages.GetArrayLength() > 0 &&
                messages[0].TryGetProperty("id", out var id))
                return id.GetString();
        }
        catch (JsonException)
        {
        }
        return null;
    }

    /// <summary>Graph hata gövdesi: {"error":{"message","type","code","error_subcode","fbtrace_id"}}.</summary>
    private static string? ParseGraphError(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("error", out var error))
                return null;

            var code = error.TryGetProperty("code", out var c) ? c.ToString() : "?";
            var subcode = error.TryGetProperty("error_subcode", out var sc) ? "/" + sc : "";
            var msg = error.TryGetProperty("message", out var m) ? m.GetString() : null;
            var trace = error.TryGetProperty("fbtrace_id", out var t) ? $" (fbtrace_id: {t.GetString()})" : "";
            return $"Meta hata {code}{subcode}: {msg}{trace}";
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Truncate(string value)
        => value.Length <= 200 ? value : value[..200] + "...";
}
