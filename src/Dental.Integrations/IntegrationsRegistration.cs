using Dental.Application.Abstractions;
using Dental.Integrations.Common;
using Dental.Integrations.EInvoice.Fake;
using Dental.Integrations.EInvoice.Nes;
using Dental.Integrations.EInvoice.Uyumsoft;
using Dental.Integrations.Enabiz;
using Dental.Integrations.Enabiz.Fake;
using Dental.Integrations.Payments.Fake;
using Dental.Integrations.Payments.Iyzico;
using Dental.Integrations.Sms.Fake;
using Dental.Integrations.Sms.Netgsm;
using Dental.Integrations.WhatsApp.Fake;
using Dental.Integrations.WhatsApp.MetaCloud;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dental.Integrations;

/// <summary>
/// Entegrasyon sürücülerinin keyed DI kayıtları (design-2 §1.0).
///
/// Kalıp her aile için aynıdır:
/// (1) ayar POCO'su scoped kaydedilir ve appsettings'teki sistem geneli uçlarla doldurulur,
/// (2) gerçek sürücü typed HttpClient ile kaydedilir,
/// (3) port arayüzü sağlayıcı anahtarıyla (keyed) bağlanır,
/// (4) <see cref="IntegrationProviderOptions"/> hangi portun hangi entegrasyon anahtarına ve
///     varsayılan sürücüye karşılık geldiğini bildirir.
///
/// Tenant'a özel kimlik bilgileri DI'da DEĞİL, <see cref="IIntegrationProviderFactory"/> çözümü
/// sırasında TenantIntegrationSettings'ten okunup ayar nesnesine uygulanır.
/// </summary>
public static class IntegrationsRegistration
{
    public const string FakeKey = "fake";

    public static IServiceCollection AddIntegrationDrivers(
        this IServiceCollection services, IConfiguration configuration)
    {
        var options = new IntegrationProviderOptions();

        AddEInvoice(services, configuration, options);
        AddEnabiz(services, configuration, options);
        AddSms(services, configuration, options);
        AddWhatsApp(services, configuration, options);
        AddPayments(services, configuration, options);

        services.AddSingleton(options);
        services.AddScoped<IIntegrationProviderFactory, IntegrationProviderFactory>();
        return services;
    }

    // ---- e-Belge (F aşaması) ----

    private static void AddEInvoice(
        IServiceCollection services, IConfiguration configuration, IntegrationProviderOptions options)
    {
        services.AddScoped(_ => new UyumsoftSettings
        {
            TestUrl = Value(configuration, "Integrations:EInvoice:Uyumsoft:TestUrl",
                "https://efatura-test.uyumsoft.com.tr/services/Integration"),
            LiveUrl = Value(configuration, "Integrations:EInvoice:Uyumsoft:LiveUrl",
                "https://efatura.uyumsoft.com.tr/services/Integration"),
            SmmUrl = configuration["Integrations:EInvoice:Uyumsoft:SmmUrl"],
        });
        services.AddScoped(_ => new NesSettings
        {
            TestUrl = Value(configuration, "Integrations:EInvoice:Nes:TestUrl", "https://apitest.nes.com.tr"),
            LiveUrl = Value(configuration, "Integrations:EInvoice:Nes:LiveUrl", "https://api.nes.com.tr"),
        });

        // e-belge gönderimleri büyük XML taşır; taşıma zaman aşımı SMS/WA'dan uzun tutulur.
        services.AddHttpClient<UyumsoftEInvoiceProvider>(c => c.Timeout = TimeSpan.FromSeconds(90));
        services.AddHttpClient<NesEInvoiceProvider>(c => c.Timeout = TimeSpan.FromSeconds(90));
        services.AddSingleton<FakeEInvoiceProvider>();

        services.AddKeyedScoped<IEInvoiceProvider>("uyumsoft",
            (sp, _) => sp.GetRequiredService<UyumsoftEInvoiceProvider>());
        services.AddKeyedScoped<IEInvoiceProvider>("nes",
            (sp, _) => sp.GetRequiredService<NesEInvoiceProvider>());
        services.AddKeyedSingleton<IEInvoiceProvider>(FakeKey,
            (sp, _) => sp.GetRequiredService<FakeEInvoiceProvider>());

        options.Register<IEInvoiceProvider>(
            integrationKey: "EInvoice",
            defaultProviderKey: Value(configuration, "Integrations:DefaultEInvoiceProvider", FakeKey),
            settingsTypes: new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
            {
                ["uyumsoft"] = typeof(UyumsoftSettings),
                ["nes"] = typeof(NesSettings),
            },
            providerKeys: ["uyumsoft", "nes", FakeKey],
            forcedProviderKey: configuration["Integrations:ProviderOverride:EInvoice"]);
    }

    // ---- e-Nabız / USS (H aşaması) ----

    private static void AddEnabiz(
        IServiceCollection services, IConfiguration configuration, IntegrationProviderOptions options)
    {
        var testUrl = Value(configuration, "Integrations:Enabiz:SysTestUrl",
            "https://systest.sagliknet.saglik.gov.tr/SYS/SYSWS.svc");
        var liveUrl = Value(configuration, "Integrations:Enabiz:SysLiveUrl",
            "https://sys.sagliknet.saglik.gov.tr/SYS/SYSWS.svc");
        var allowInvalidTestCertificate =
            configuration.GetValue("Integrations:Enabiz:AllowInvalidTestCertificate", false);

        services.AddScoped(_ => new EnabizSettings
        {
            SysTestUrl = testUrl,
            SysLiveUrl = liveUrl,
            SkrsBaseUrl = Value(configuration, "Integrations:Enabiz:SkrsUrl",
                "https://skrs.saglik.gov.tr/api/SkrsService"),
            EmbedPayloadAsRawXml =
                configuration.GetValue("Integrations:Enabiz:EmbedPayloadAsRawXml", false),
            // appsettings'te boş string olarak duran anahtarlar "değer yok" demektir; boş string
            // pakete yazılırsa healthcareProvider value="" gibi anlamsız alanlar oluşur.
            FacilityName = NullIfBlank(configuration["Integrations:Enabiz:FacilityName"]),
            SoftwareCompanyCode = NullIfBlank(configuration["Integrations:Enabiz:SoftwareCompanyCode"]),
        });

        services.AddHttpClient<SysSoapClient>(c => c.Timeout = TimeSpan.FromSeconds(90))
            .ConfigurePrimaryHttpMessageHandler(() =>
                BuildSysHandler(allowInvalidTestCertificate, testUrl, liveUrl));

        services.AddHttpClient<SkrsSyncService>(c => c.Timeout = TimeSpan.FromSeconds(120));
        services.AddSingleton<FakeEnabizClient>();

        services.AddKeyedScoped<IEnabizClient>("sys", (sp, _) => sp.GetRequiredService<SysSoapClient>());
        services.AddKeyedSingleton<IEnabizClient>(FakeKey, (sp, _) => sp.GetRequiredService<FakeEnabizClient>());

        options.Register<IEnabizClient>(
            integrationKey: "Enabiz",
            defaultProviderKey: Value(configuration, "Integrations:DefaultEnabizProvider", FakeKey),
            settingsTypes: new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
            {
                ["sys"] = typeof(EnabizSettings),
            },
            providerKeys: ["sys", FakeKey],
            forcedProviderKey: configuration["Integrations:ProviderOverride:Enabiz"]);
    }

    /// <summary>
    /// SYS taşıma handler'ı.
    ///
    /// <para><b>Sertifika istisnası:</b> <c>systest.sagliknet.saglik.gov.tr</c> sunucusunun TLS
    /// sertifikasının SÜRESİ DOLMUŞTUR (ölçüldü) — bu yüzden test ortamına .NET varsayılan
    /// doğrulamasıyla bağlanılamaz. Gevşetme İKİ koşula birden bağlıdır:
    /// <list type="number">
    ///   <item><c>Integrations:Enabiz:AllowInvalidTestCertificate</c> açıkça true olmalı (varsayılan false),</item>
    ///   <item>istek YALNIZCA yapılandırılmış TEST host'una gitmeli — canlı host her zaman tam
    ///         doğrulamadan geçer.</item>
    /// </list>
    /// İkinci koşul kritiktir: bayrak yanlışlıkla üretimde açılsa bile canlı uç korunur, çünkü
    /// karar isteğin gerçek host'una bakılarak verilir (çalışma zamanı ortam değişkenine değil).</para>
    /// </summary>
    private static HttpMessageHandler BuildSysHandler(bool allowInvalidTestCertificate, string testUrl, string liveUrl)
    {
        var handler = new HttpClientHandler();
        if (!allowInvalidTestCertificate) return handler;

        var testHost = SafeHost(testUrl);
        var liveHost = SafeHost(liveUrl);
        if (testHost is null || string.Equals(testHost, liveHost, StringComparison.OrdinalIgnoreCase))
            return handler;

        handler.ServerCertificateCustomValidationCallback = (request, _, _, errors) =>
        {
            if (errors == System.Net.Security.SslPolicyErrors.None) return true;
            return string.Equals(request.RequestUri?.Host, testHost, StringComparison.OrdinalIgnoreCase);
        };
        return handler;
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? SafeHost(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri) ? uri.Host : null;

    // ---- SMS (G aşamasına hazırlık) ----

    private static void AddSms(
        IServiceCollection services, IConfiguration configuration, IntegrationProviderOptions options)
    {
        services.AddScoped(_ => new NetgsmSettings
        {
            BaseUrl = Value(configuration, "Integrations:Sms:Netgsm:BaseUrl", "https://api.netgsm.com.tr"),
        });

        services.AddHttpClient<NetgsmSmsProvider>(c => c.Timeout = TimeSpan.FromSeconds(30));
        services.AddSingleton<FakeSmsProvider>();

        services.AddKeyedScoped<ISmsProvider>("netgsm", (sp, _) => sp.GetRequiredService<NetgsmSmsProvider>());
        services.AddKeyedSingleton<ISmsProvider>(FakeKey, (sp, _) => sp.GetRequiredService<FakeSmsProvider>());

        var defaultSms = Value(configuration, "Integrations:DefaultSmsProvider", FakeKey);
        options.Register<ISmsProvider>(
            integrationKey: "Sms",
            defaultProviderKey: defaultSms,
            settingsTypes: new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
            {
                ["netgsm"] = typeof(NetgsmSettings),
            },
            providerKeys: ["netgsm", FakeKey],
            forcedProviderKey: configuration["Integrations:ProviderOverride:Sms"]);

        // Tenant bağlamı olmayan çağrı yerleri (ConsentService) için varsayılan sürücü;
        // Program.cs'teki geçici düz kaydın yerini alır, FakeSmsProvider davranışı korunur.
        services.AddScoped<ISmsProvider>(sp => sp.GetRequiredKeyedService<ISmsProvider>(defaultSms));
    }

    // ---- WhatsApp ----

    private static void AddWhatsApp(
        IServiceCollection services, IConfiguration configuration, IntegrationProviderOptions options)
    {
        services.AddScoped(_ => new MetaWhatsAppSettings
        {
            GraphApiBase = Value(configuration, "Integrations:WhatsApp:GraphApiBase",
                "https://graph.facebook.com/v21.0"),
        });

        services.AddHttpClient<MetaWhatsAppProvider>(c => c.Timeout = TimeSpan.FromSeconds(30));
        services.AddSingleton<FakeWhatsAppProvider>();

        services.AddKeyedScoped<IWhatsAppProvider>("meta", (sp, _) => sp.GetRequiredService<MetaWhatsAppProvider>());
        services.AddKeyedSingleton<IWhatsAppProvider>(FakeKey, (sp, _) => sp.GetRequiredService<FakeWhatsAppProvider>());

        options.Register<IWhatsAppProvider>(
            integrationKey: "WhatsApp",
            defaultProviderKey: Value(configuration, "Integrations:DefaultWhatsAppProvider", FakeKey),
            settingsTypes: new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
            {
                ["meta"] = typeof(MetaWhatsAppSettings),
            },
            providerKeys: ["meta", FakeKey],
            forcedProviderKey: configuration["Integrations:ProviderOverride:WhatsApp"]);
    }

    // ---- Ödeme ----

    private static void AddPayments(
        IServiceCollection services, IConfiguration configuration, IntegrationProviderOptions options)
    {
        services.AddScoped(_ => new IyzicoSettings
        {
            BaseUrl = Value(configuration, "Integrations:Payments:Iyzico:SandboxUrl",
                "https://sandbox-api.iyzipay.com"),
        });

        services.AddScoped<IyzicoPaymentProvider>();
        // Bellek içi durum tuttuğu için MUTLAKA singleton; ctor'un ikinci parametresi
        // (sahte ödeme sayfası taban URL'i) DI'dan çözülemediği için elle verilir.
        services.AddSingleton(sp => new FakePaymentProvider(
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<FakePaymentProvider>>(),
            Value(configuration, "Public:BaseUrl", "http://localhost:4200").TrimEnd('/') + "/dev/fake-payment"));

        services.AddKeyedScoped<IPaymentProvider>("iyzico", (sp, _) => sp.GetRequiredService<IyzicoPaymentProvider>());
        services.AddKeyedSingleton<IPaymentProvider>(FakeKey, (sp, _) => sp.GetRequiredService<FakePaymentProvider>());

        options.Register<IPaymentProvider>(
            integrationKey: "Payment",
            defaultProviderKey: Value(configuration, "Integrations:DefaultPaymentProvider", FakeKey),
            settingsTypes: new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase)
            {
                ["iyzico"] = typeof(IyzicoSettings),
            },
            providerKeys: ["iyzico", FakeKey],
            forcedProviderKey: configuration["Integrations:ProviderOverride:Payment"]);
    }

    private static string Value(IConfiguration configuration, string key, string fallback)
    {
        var value = configuration[key];
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }
}
