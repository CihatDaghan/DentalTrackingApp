using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;
using Xunit;

namespace Dental.IntegrationTests;

/// <summary>
/// Testcontainers MSSQL + gerçek uygulama host'u. Development ortamı kullanılır:
/// başlangıçta DB oluşturma + migration + seed (demo kiracı) gerçek yoldan koşar.
/// Apple Silicon'da Docker Desktop "Use Rosetta" gerektirir (amd64 imaj).
/// </summary>
public sealed class ApiFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _sql = new MsSqlBuilder()
        .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    public const string WhatsAppAppSecret = "test-wa-app-secret";
    public const string WhatsAppVerifyToken = "test-wa-verify-token";

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;
    public HttpClient Client { get; private set; } = null!;
    public IServiceProvider Services => Factory.Services;

    public async Task InitializeAsync()
    {
        await _sql.StartAsync();
        var cs = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(_sql.GetConnectionString())
        {
            InitialCatalog = "DentalAppTest",
        };
        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("ConnectionStrings:Default", cs.ConnectionString);
            // Testler tek IP'den art arda login yapar; auth hız sınırını gevşet.
            builder.UseSetting("RateLimiting:AuthPermitLimit", "1000");
            // Demo kiracı seed'i e-belge sürücüsü olarak Uyumsoft TEST'i işaretler; testler
            // dış servise çıkmadan koşmalı, bu yüzden sürücü ortam düzeyinde fake'e zorlanır.
            builder.UseSetting("Integrations:ProviderOverride:EInvoice", "fake");
            // G: mesajlaşma/ödeme sürücüleri de dış servise çıkmadan koşar.
            builder.UseSetting("Integrations:ProviderOverride:Sms", "fake");
            builder.UseSetting("Integrations:ProviderOverride:WhatsApp", "fake");
            builder.UseSetting("Integrations:ProviderOverride:Payment", "fake");
            // H: e-Nabız sürücüsü de sahteye zorlanır — testler Bakanlık sunucusuna ÇIKMAZ.
            builder.UseSetting("Integrations:ProviderOverride:Enabiz", "fake");
            // WhatsApp webhook imza/doğrulama testleri için bilinen sırlar.
            builder.UseSetting("Integrations:WhatsApp:AppSecret", WhatsAppAppSecret);
            builder.UseSetting("Integrations:WhatsApp:VerifyToken", WhatsAppVerifyToken);
            // Public uçlara (ödeme sayfası + webhook) yoğun test trafiği; hız sınırını gevşet.
            builder.UseSetting("RateLimiting:PublicPermitLimit", "1000");
        });
        Client = Factory.CreateClient();
        // Host'u ayağa kaldırıp migrate+seed'in bitmesini garanti et.
        using var scope = Factory.Services.CreateScope();
        _ = scope.ServiceProvider.GetRequiredService<Dental.Infrastructure.Persistence.AppDbContext>();
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _sql.DisposeAsync();
    }
}
