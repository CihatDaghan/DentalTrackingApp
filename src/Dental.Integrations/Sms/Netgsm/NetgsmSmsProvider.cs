using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Dental.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace Dental.Integrations.Sms.Netgsm;

/// <summary>
/// Netgsm REST v2 SMS sürücüsü. Typed client desenine uygun: HttpClient dışarıdan gelir,
/// retry/timeout politikası üst katmanda (Http.Resilience + outbox) kuruludur; burada retry YOKTUR.
/// </summary>
public sealed class NetgsmSmsProvider(HttpClient http, NetgsmSettings settings, ILogger<NetgsmSmsProvider> logger) : ISmsProvider
{
    // Netgsm REST v2 yanıt kodları → anlamlı hata metinleri.
    private static readonly IReadOnlyDictionary<string, string> ErrorCodes = new Dictionary<string, string>
    {
        ["20"] = "Mesaj metni hatalı veya karakter sınırı aşıldı (20)",
        ["30"] = "Geçersiz kullanıcı adı/şifre veya API erişim izni yok; IP kısıtlaması olabilir (30)",
        ["40"] = "Gönderici adı (msgheader) sistemde tanımlı değil (40)",
        ["50"] = "Abone hesabıyla İYS kontrollü gönderim yapılamaz (50)",
        ["51"] = "Aboneliğe ait İYS marka bilgisi bulunamadı (51)",
        ["70"] = "Hatalı veya eksik parametre (70)",
        ["80"] = "Gönderim sınır aşımı (80)",
        ["85"] = "Mükerrer gönderim sınır aşımı (85)",
    };

    public async Task<SmsSendResult> SendAsync(SmsMessage message, CancellationToken ct = default)
    {
        var payload = new Dictionary<string, object>
        {
            ["msgheader"] = string.IsNullOrWhiteSpace(message.Header) ? settings.MsgHeader : message.Header,
            ["messages"] = new[] { new Dictionary<string, string> { ["msg"] = message.Body, ["no"] = message.To } },
            ["encoding"] = "TR",
        };

        // İYS kuralı: yalnız ticari (kampanya) mesajlarda İYS filtresi gönderilir;
        // bilgilendirme (randevu/ödeme hatırlatma) mesajları İYS kapsamı dışındadır.
        if (message.Kind == SmsKind.Commercial)
            payload["iysfilter"] = "11";

        using var request = new HttpRequestMessage(HttpMethod.Post, BuildUri("sms/rest/v2/send"))
        {
            Content = JsonContent.Create(payload),
        };
        request.Headers.Authorization = BasicAuth();

        string body;
        try
        {
            using var response = await http.SendAsync(request, ct).ConfigureAwait(false);
            body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            var parsed = TryParseResponse(body);
            if (parsed is null)
            {
                throw new SmsProviderException(
                    $"Netgsm yanıtı çözümlenemedi (HTTP {(int)response.StatusCode}): {Truncate(body)}");
            }

            var (code, jobId, description) = parsed.Value;
            if (code == "00")
            {
                logger.LogInformation("Netgsm SMS gönderildi. JobId={JobId} ClientRef={ClientRef}", jobId, message.ClientRef);
                return new SmsSendResult(Success: true, ProviderMessageId: jobId);
            }

            var error = ErrorCodes.TryGetValue(code, out var known)
                ? known
                : $"Netgsm hata kodu: {code}" + (string.IsNullOrWhiteSpace(description) ? "" : $" ({description})");
            logger.LogWarning("Netgsm SMS reddedildi. Code={Code} ClientRef={ClientRef}", code, message.ClientRef);
            return new SmsSendResult(Success: false, Error: error);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException ex)
        {
            throw new SmsProviderException("Netgsm isteği zaman aşımına uğradı.", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new SmsProviderException($"Netgsm isteği başarısız: {ex.Message}", ex);
        }
    }

    public async Task<decimal> GetBalanceAsync(CancellationToken ct = default)
    {
        // Klasik bakiye ucu: "00 <kredi>" düz metin döner; hata durumunda yalnız kod döner.
        var uri = BuildUri($"balance/list/get?usercode={Uri.EscapeDataString(settings.UserCode)}&password={Uri.EscapeDataString(settings.Password)}");
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.Authorization = BasicAuth();
            using var response = await http.SendAsync(request, ct).ConfigureAwait(false);
            var body = (await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false)).Trim();

            if (body.StartsWith("00", StringComparison.Ordinal))
            {
                var rest = body[2..].Trim().Replace(',', '.');
                if (decimal.TryParse(rest, NumberStyles.Number, CultureInfo.InvariantCulture, out var balance))
                    return balance;
            }

            throw new SmsProviderException($"Netgsm bakiye sorgusu başarısız: {Truncate(body)}");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException ex)
        {
            throw new SmsProviderException("Netgsm bakiye isteği zaman aşımına uğradı.", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new SmsProviderException($"Netgsm bakiye isteği başarısız: {ex.Message}", ex);
        }
    }

    private Uri BuildUri(string relative)
    {
        if (!string.IsNullOrWhiteSpace(settings.BaseUrl))
            return new Uri(new Uri(settings.BaseUrl.TrimEnd('/') + "/"), relative);
        return http.BaseAddress is not null
            ? new Uri(http.BaseAddress, relative)
            : throw new SmsProviderException("Netgsm BaseUrl ayarı eksik.");
    }

    private AuthenticationHeaderValue BasicAuth()
        => new("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{settings.UserCode}:{settings.Password}")));

    private static (string Code, string? JobId, string? Description)? TryParseResponse(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("code", out var code))
                return null;
            return (
                code.ValueKind == JsonValueKind.String ? code.GetString()! : code.ToString(),
                root.TryGetProperty("jobid", out var jobId) ? jobId.ToString() : null,
                root.TryGetProperty("description", out var desc) ? desc.ToString() : null);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string Truncate(string value)
        => value.Length <= 200 ? value : value[..200] + "...";
}
