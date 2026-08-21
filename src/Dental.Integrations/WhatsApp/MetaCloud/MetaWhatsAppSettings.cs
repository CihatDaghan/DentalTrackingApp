using System.Text.Json;
using Dental.Application.Abstractions;
using Dental.Integrations.Common;

namespace Dental.Integrations.WhatsApp.MetaCloud;

/// <summary>Tenant'ın şifreli ayar JSON'undan çözülüp factory tarafından doldurulur.</summary>
public sealed class MetaWhatsAppSettings : IIntegrationSettings
{
    /// <summary>Kalıcı sistem kullanıcısı erişim token'ı (Bearer).</summary>
    public string AccessToken { get; set; } = "";

    /// <summary>Gönderici numaranın Meta tarafındaki kimliği.</summary>
    public string PhoneNumberId { get; set; } = "";

    /// <summary>Webhook X-Hub-Signature-256 doğrulaması için uygulama sırrı.</summary>
    public string AppSecret { get; set; } = "";

    /// <summary>Sistem geneli appsettings'ten gelir (Integrations:WhatsApp:GraphApiBase).</summary>
    public string GraphApiBase { get; set; } = "https://graph.facebook.com/v21.0";

    public void Apply(string? settingsJson, string environment)
    {
        if (string.IsNullOrWhiteSpace(settingsJson)) return;

        Payload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<Payload>(settingsJson, IntegrationSettingsJson.Options);
        }
        catch (JsonException ex)
        {
            throw new WhatsAppProviderException("Meta WhatsApp ayar JSON'u çözümlenemedi.", ex);
        }

        if (payload is null) return;
        if (!string.IsNullOrWhiteSpace(payload.AccessToken)) AccessToken = payload.AccessToken;
        if (!string.IsNullOrWhiteSpace(payload.PhoneNumberId)) PhoneNumberId = payload.PhoneNumberId;
        if (!string.IsNullOrWhiteSpace(payload.AppSecret)) AppSecret = payload.AppSecret;
        if (!string.IsNullOrWhiteSpace(payload.GraphApiBase)) GraphApiBase = payload.GraphApiBase;
    }

    private sealed record Payload(string? AccessToken, string? PhoneNumberId, string? AppSecret, string? GraphApiBase);
}
