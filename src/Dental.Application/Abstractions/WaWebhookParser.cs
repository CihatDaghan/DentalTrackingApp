using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Dental.Application.Abstractions;

/// <summary>Meta Cloud API webhook'undan gelen teslim/okundu durum güncellemesi.</summary>
public sealed record WaStatusUpdate(
    string ProviderMessageId,
    string Status,
    string RecipientId,
    DateTime TimestampUtc,
    string? ErrorCode = null,
    string? ErrorDetail = null);

/// <summary>Meta Cloud API webhook'undan gelen kullanıcı mesajı (24 saatlik servis penceresini açar).</summary>
public sealed record WaIncomingMessage(
    string ProviderMessageId,
    string From,
    DateTime TimestampUtc,
    string Type,
    string? Text = null,
    string? SenderName = null);

/// <summary>Tek webhook POST gövdesinin çözülmüş hali; bir gövde birden çok olay taşıyabilir.</summary>
public sealed record WaWebhookEvent(
    string? PhoneNumberId,
    IReadOnlyList<WaStatusUpdate> StatusUpdates,
    IReadOnlyList<WaIncomingMessage> IncomingMessages);

/// <summary>
/// Meta WhatsApp webhook gövdesi çözücüsü ve imza doğrulayıcısı.
/// Sağlayıcıya HTTP çağrısı yapmaz; webhook controller'ı doğrudan kullanır.
/// </summary>
public static class WaWebhookParser
{
    /// <exception cref="WhatsAppProviderException">Gövde JSON değilse veya beklenen kökte değilse.</exception>
    public static WaWebhookEvent Parse(string json)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new WhatsAppProviderException("WhatsApp webhook gövdesi geçerli JSON değil.", ex);
        }

        using (doc)
        {
            var root = doc.RootElement;
            string? phoneNumberId = null;
            var statuses = new List<WaStatusUpdate>();
            var incoming = new List<WaIncomingMessage>();

            if (!root.TryGetProperty("entry", out var entries) || entries.ValueKind != JsonValueKind.Array)
                return new WaWebhookEvent(null, statuses, incoming);

            foreach (var entry in entries.EnumerateArray())
            {
                if (!entry.TryGetProperty("changes", out var changes) || changes.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (var change in changes.EnumerateArray())
                {
                    if (!change.TryGetProperty("value", out var value))
                        continue;

                    if (value.TryGetProperty("metadata", out var metadata) &&
                        metadata.TryGetProperty("phone_number_id", out var pni))
                        phoneNumberId = pni.GetString();

                    if (value.TryGetProperty("statuses", out var statusArr) && statusArr.ValueKind == JsonValueKind.Array)
                        foreach (var s in statusArr.EnumerateArray())
                            statuses.Add(ParseStatus(s));

                    if (value.TryGetProperty("messages", out var msgArr) && msgArr.ValueKind == JsonValueKind.Array)
                        foreach (var m in msgArr.EnumerateArray())
                            incoming.Add(ParseMessage(m, value));
                }
            }

            return new WaWebhookEvent(phoneNumberId, statuses, incoming);
        }
    }

    /// <summary>
    /// X-Hub-Signature-256 doğrulaması: gövdenin HAM baytları üzerinden app secret ile HMAC-SHA256.
    /// Başlık biçimi: "sha256=&lt;hex&gt;". Sabit zamanlı karşılaştırma kullanılır.
    /// </summary>
    public static bool VerifySignature(ReadOnlySpan<byte> payloadBytes, string? headerSignature, string appSecret)
    {
        if (string.IsNullOrWhiteSpace(headerSignature) || string.IsNullOrEmpty(appSecret))
            return false;

        const string prefix = "sha256=";
        var value = headerSignature.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
            ? headerSignature[prefix.Length..]
            : headerSignature;

        byte[] expected;
        try
        {
            expected = Convert.FromHexString(value);
        }
        catch (FormatException)
        {
            return false;
        }

        var computed = HMACSHA256.HashData(Encoding.UTF8.GetBytes(appSecret), payloadBytes);
        return CryptographicOperations.FixedTimeEquals(computed, expected);
    }

    private static WaStatusUpdate ParseStatus(JsonElement s)
    {
        string? errorCode = null;
        string? errorDetail = null;
        if (s.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array)
        {
            foreach (var e in errors.EnumerateArray())
            {
                errorCode = e.TryGetProperty("code", out var c) ? c.ToString() : errorCode;
                errorDetail = e.TryGetProperty("title", out var t) ? t.GetString() : errorDetail;
                if (e.TryGetProperty("message", out var em) && !string.IsNullOrEmpty(em.GetString()))
                    errorDetail = em.GetString();
                break;
            }
        }

        return new WaStatusUpdate(
            ProviderMessageId: GetString(s, "id") ?? "",
            Status: GetString(s, "status") ?? "",
            RecipientId: GetString(s, "recipient_id") ?? "",
            TimestampUtc: ParseUnixTimestamp(GetString(s, "timestamp")),
            ErrorCode: errorCode,
            ErrorDetail: errorDetail);
    }

    private static WaIncomingMessage ParseMessage(JsonElement m, JsonElement value)
    {
        string? text = null;
        if (m.TryGetProperty("text", out var textEl) && textEl.TryGetProperty("body", out var body))
            text = body.GetString();

        string? senderName = null;
        var from = GetString(m, "from");
        if (value.TryGetProperty("contacts", out var contacts) && contacts.ValueKind == JsonValueKind.Array)
        {
            foreach (var c in contacts.EnumerateArray())
            {
                if (GetString(c, "wa_id") != from && contacts.GetArrayLength() > 1)
                    continue;
                if (c.TryGetProperty("profile", out var profile))
                    senderName = GetString(profile, "name");
                break;
            }
        }

        return new WaIncomingMessage(
            ProviderMessageId: GetString(m, "id") ?? "",
            From: from ?? "",
            TimestampUtc: ParseUnixTimestamp(GetString(m, "timestamp")),
            Type: GetString(m, "type") ?? "unknown",
            Text: text,
            SenderName: senderName);
    }

    private static string? GetString(JsonElement el, string name)
        => el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    private static DateTime ParseUnixTimestamp(string? unixSeconds)
        => long.TryParse(unixSeconds, out var s)
            ? DateTimeOffset.FromUnixTimeSeconds(s).UtcDateTime
            : DateTime.MinValue;
}
