namespace Dental.Infrastructure.Consents;

/// <summary>Public (auth'suz) sayfaların taban adresi — SMS onam linki buradan üretilir (dev: Angular 4200).</summary>
public sealed class PublicOptions
{
    public const string SectionName = "Public";

    public string BaseUrl { get; set; } = "http://localhost:4200";

    /// <summary>API'nin dışarıdan erişilebilir adresi — sağlayıcı callback'i (iyzico) buraya döner.</summary>
    public string ApiBaseUrl { get; set; } = "http://localhost:5210";
}
