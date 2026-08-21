using System.Text.Json;
using Dental.Application.Abstractions;

namespace Dental.Integrations.EInvoice.Nes;

/// <summary>
/// NES (REST + JWT) e-belge sürücüsü ayarları.
/// Endpoint'ler appsettings'ten (Integrations:EInvoice:Nes:TestUrl/LiveUrl), kimlik bilgileri
/// tenant'ın şifreli ayar JSON'undan gelir.
/// </summary>
public sealed class NesSettings : IIntegrationSettings
{
    public string Username { get; set; } = "";

    public string Password { get; set; } = "";

    public string TestUrl { get; set; } = "https://apitest.nes.com.tr";

    public string LiveUrl { get; set; } = "https://api.nes.com.tr";

    public string Environment { get; set; } = "Test";

    /// <summary>Gönderici VKN/TCKN — belge başlığında kullanılır.</summary>
    public string? SenderVknTckn { get; set; }

    public string BaseUrl =>
        string.Equals(Environment, "Live", StringComparison.OrdinalIgnoreCase) ? LiveUrl : TestUrl;

    public void Apply(string? settingsJson, string environment)
    {
        Environment = string.IsNullOrWhiteSpace(environment) ? "Test" : environment;
        if (string.IsNullOrWhiteSpace(settingsJson)) return;

        Payload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<Payload>(settingsJson, JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new EInvoiceProviderException("NES ayar JSON'u çözümlenemedi.", ex);
        }

        if (payload is null) return;
        if (!string.IsNullOrWhiteSpace(payload.Username)) Username = payload.Username;
        if (!string.IsNullOrWhiteSpace(payload.Password)) Password = payload.Password;
        if (!string.IsNullOrWhiteSpace(payload.TestUrl)) TestUrl = payload.TestUrl;
        if (!string.IsNullOrWhiteSpace(payload.LiveUrl)) LiveUrl = payload.LiveUrl;
        if (!string.IsNullOrWhiteSpace(payload.SenderVknTckn)) SenderVknTckn = payload.SenderVknTckn;
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private sealed record Payload(
        string? Username,
        string? Password,
        string? TestUrl,
        string? LiveUrl,
        string? SenderVknTckn);
}
