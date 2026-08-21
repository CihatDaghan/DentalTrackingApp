using System.Text.Json;
using Dental.Application.Abstractions;
using Dental.Integrations.Common;

namespace Dental.Integrations.Payments.Iyzico;

/// <summary>Tenant'ın şifreli ayar JSON'undan çözülüp factory tarafından doldurulur.</summary>
public sealed class IyzicoSettings : IIntegrationSettings
{
    public string ApiKey { get; set; } = "";

    public string SecretKey { get; set; } = "";

    /// <summary>Sandbox: https://sandbox-api.iyzipay.com — Canlı: https://api.iyzipay.com (appsettings'ten).</summary>
    public string BaseUrl { get; set; } = "https://sandbox-api.iyzipay.com";

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
            throw new PaymentProviderException("iyzico ayar JSON'u çözümlenemedi.", ex);
        }

        if (payload is null) return;
        if (!string.IsNullOrWhiteSpace(payload.ApiKey)) ApiKey = payload.ApiKey;
        if (!string.IsNullOrWhiteSpace(payload.SecretKey)) SecretKey = payload.SecretKey;
        if (!string.IsNullOrWhiteSpace(payload.BaseUrl)) BaseUrl = payload.BaseUrl;
    }

    private sealed record Payload(string? ApiKey, string? SecretKey, string? BaseUrl);
}
