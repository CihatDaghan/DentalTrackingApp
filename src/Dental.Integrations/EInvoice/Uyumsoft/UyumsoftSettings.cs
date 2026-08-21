using System.Text.Json;
using Dental.Application.Abstractions;

namespace Dental.Integrations.EInvoice.Uyumsoft;

/// <summary>
/// Uyumsoft e-belge sürücüsü ayarları. Endpoint'ler appsettings'ten
/// (Integrations:EInvoice:Uyumsoft:TestUrl/LiveUrl), kimlik bilgileri tenant'ın şifreli
/// ayar JSON'undan gelir ve <see cref="Apply"/> ile doldurulur.
/// </summary>
public sealed class UyumsoftSettings : IIntegrationSettings
{
    /// <summary>Uyumsoft test ortamı anonim kimliği: Uyumsoft / Uyumsoft.</summary>
    public string Username { get; set; } = "Uyumsoft";

    public string Password { get; set; } = "Uyumsoft";

    public string TestUrl { get; set; } = "https://efatura-test.uyumsoft.com.tr/services/Integration";

    public string LiveUrl { get; set; } = "https://efatura.uyumsoft.com.tr/services/Integration";

    /// <summary>'Test' | 'Live'</summary>
    public string Environment { get; set; } = "Test";

    /// <summary>
    /// e-SMM ayrı bir servis ucundan yürür; Integration servisi yalnız Invoice (e-Fatura/e-Arşiv)
    /// taşır. Uç adresi doğrulanana kadar boştur — boşken e-SMM gönderimi açıkça reddedilir.
    /// </summary>
    public string? SmmUrl { get; set; }

    public string EndpointUrl =>
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
            throw new EInvoiceProviderException("Uyumsoft ayar JSON'u çözümlenemedi.", ex);
        }

        if (payload is null) return;
        if (!string.IsNullOrWhiteSpace(payload.Username)) Username = payload.Username;
        if (!string.IsNullOrWhiteSpace(payload.Password)) Password = payload.Password;
        if (!string.IsNullOrWhiteSpace(payload.TestUrl)) TestUrl = payload.TestUrl;
        if (!string.IsNullOrWhiteSpace(payload.LiveUrl)) LiveUrl = payload.LiveUrl;
        if (!string.IsNullOrWhiteSpace(payload.SmmUrl)) SmmUrl = payload.SmmUrl;
    }

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private sealed record Payload(
        string? Username,
        string? Password,
        string? TestUrl,
        string? LiveUrl,
        string? SmmUrl);
}
