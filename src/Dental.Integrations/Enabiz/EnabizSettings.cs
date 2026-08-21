using System.Text.Json;
using Dental.Application.Abstractions;
using Dental.Domain.Enums;
using Dental.Integrations.Common;

namespace Dental.Integrations.Enabiz;

/// <summary>
/// e-Nabız/USS sürücü ayarları. Uç adresleri appsettings'ten
/// (<c>Integrations:Enabiz:SysTestUrl</c> / <c>SysLiveUrl</c> / <c>SkrsUrl</c>), kimlik bilgileri
/// tenant'ın şifreli ayar JSON'undan gelir: <c>{ckysCode, ussUsername, ussPassword, applicationCode, mode}</c>.
/// </summary>
public sealed class EnabizSettings : IIntegrationSettings
{
    public string SysTestUrl { get; set; } = "https://systest.sagliknet.saglik.gov.tr/SYS/SYSWS.svc";

    public string SysLiveUrl { get; set; } = "https://sys.sagliknet.saglik.gov.tr/SYS/SYSWS.svc";

    public string SkrsBaseUrl { get; set; } = "https://skrs.saglik.gov.tr/api/SkrsService";

    /// <summary>ÇKYS tesis kodu.</summary>
    public string? CkysCode { get; set; }

    /// <summary>USS kullanıcı adı (İl Sağlık Müdürlüğü'nden alınır).</summary>
    public string? UssUsername { get; set; }

    public string? UssPassword { get; set; }

    /// <summary>Sağlık.NET uygulama kodu (SKRS header'ı <c>UygulamaKodu</c>).</summary>
    public string? ApplicationCode { get; set; }

    /// <summary>Kiracının gönderim modu; ayar JSON'unda saklanır.</summary>
    public EnabizMode Mode { get; set; } = EnabizMode.Held;

    /// <summary>
    /// Paket XML'inin <c>input</c> içine iç içe (kaçışlanmamış) yazılıp yazılmayacağı.
    /// Varsayılan false = WSDL'in dediği gibi kaçışlanmış string. Ayrıntı için
    /// <see cref="SysSoapClient.BuildInput"/> belgelemesine bakınız.
    /// </summary>
    public bool EmbedPayloadAsRawXml { get; set; }

    /// <summary>Tesis adı — paket başlığındaki healthcareProvider değeri.</summary>
    public string? FacilityName { get; set; }

    /// <summary>KTS'de kayıtlı yazılım firması kodu (paket başlığındaki firmaKodu).</summary>
    public string? SoftwareCompanyCode { get; set; }

    /// <summary>'Test' | 'Live'</summary>
    public string Environment { get; set; } = "Test";

    public bool IsLive => string.Equals(Environment, "Live", StringComparison.OrdinalIgnoreCase);

    public string EndpointUrl => IsLive ? SysLiveUrl : SysTestUrl;

    public bool HasCredentials =>
        !string.IsNullOrWhiteSpace(UssUsername) && !string.IsNullOrWhiteSpace(UssPassword);

    public void Apply(string? settingsJson, string environment)
    {
        Environment = string.IsNullOrWhiteSpace(environment) ? "Test" : environment;
        if (string.IsNullOrWhiteSpace(settingsJson)) return;

        Payload? payload;
        try
        {
            payload = JsonSerializer.Deserialize<Payload>(settingsJson, IntegrationSettingsJson.Options);
        }
        catch (JsonException ex)
        {
            throw new EnabizClientException("e-Nabız ayar JSON'u çözümlenemedi.", ex);
        }

        if (payload is null) return;
        if (!string.IsNullOrWhiteSpace(payload.CkysCode)) CkysCode = payload.CkysCode;
        if (!string.IsNullOrWhiteSpace(payload.UssUsername)) UssUsername = payload.UssUsername;
        if (!string.IsNullOrWhiteSpace(payload.UssPassword)) UssPassword = payload.UssPassword;
        if (!string.IsNullOrWhiteSpace(payload.ApplicationCode)) ApplicationCode = payload.ApplicationCode;
        if (payload.Mode is { } mode && Enum.IsDefined(mode)) Mode = mode;
    }

    /// <summary>Tenant ayar JSON'unun şeması — ayar ekranı da aynı alanları yazar.</summary>
    public sealed record Payload(
        string? CkysCode,
        string? UssUsername,
        string? UssPassword,
        string? ApplicationCode,
        EnabizMode? Mode);
}
