using System.Text.Json;
using Dental.Application.Abstractions;
using Dental.Integrations.Common;

namespace Dental.Integrations.Sms.Netgsm;

/// <summary>Tenant'ın şifreli ayar JSON'undan çözülüp factory tarafından doldurulur.</summary>
public sealed class NetgsmSettings : IIntegrationSettings
{
    /// <summary>Abone numarası (usercode).</summary>
    public string UserCode { get; set; } = "";

    /// <summary>API alt kullanıcı şifresi.</summary>
    public string Password { get; set; } = "";

    /// <summary>Onaylı gönderici başlığı; mesajda başlık verilmezse bu kullanılır.</summary>
    public string MsgHeader { get; set; } = "";

    /// <summary>Sistem geneli appsettings'ten gelir (Integrations:Sms:Netgsm:BaseUrl).</summary>
    public string BaseUrl { get; set; } = "https://api.netgsm.com.tr";

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
            throw new SmsProviderException("Netgsm ayar JSON'u çözümlenemedi.", ex);
        }

        if (payload is null) return;
        if (!string.IsNullOrWhiteSpace(payload.UserCode)) UserCode = payload.UserCode;
        if (!string.IsNullOrWhiteSpace(payload.Password)) Password = payload.Password;
        if (!string.IsNullOrWhiteSpace(payload.MsgHeader)) MsgHeader = payload.MsgHeader;
        if (!string.IsNullOrWhiteSpace(payload.BaseUrl)) BaseUrl = payload.BaseUrl;
    }

    private sealed record Payload(string? UserCode, string? Password, string? MsgHeader, string? BaseUrl);
}
