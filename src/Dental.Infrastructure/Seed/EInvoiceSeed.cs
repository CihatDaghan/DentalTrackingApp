using System.Text.Json;
using Dental.Application.Abstractions;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dental.Infrastructure.Seed;

/// <summary>
/// F aşaması demo verisi: demo kiracının vergi kimliği (e-belge kesebilmesi için zorunlu),
/// Uyumsoft TEST entegrasyon ayarı (şifreli) ve GİB mükellef aynası örnek kayıtları.
/// Idempotent — eksik olanı tamamlar, mevcut olanı değiştirmez.
/// </summary>
public static class EInvoiceSeed
{
    /// <summary>Uyumsoft test ortamının herkese açık anonim kimliği.</summary>
    public const string UyumsoftTestUser = "Uyumsoft";

    public static async Task ApplyAsync(
        IServiceProvider sp, AppDbContext db, ILogger logger, CancellationToken ct = default)
    {
        var demoTenantId = await db.Users.IgnoreQueryFilters()
            .Where(u => u.NormalizedEmail == DbSeeder.DemoEmail.ToUpperInvariant())
            .Select(u => u.TenantId)
            .FirstOrDefaultAsync(ct);
        if (demoTenantId is not { } tenantId) return;

        var tenant = await db.Tenants.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == tenantId, ct);
        if (tenant is null) return;

        // Vergi kimliği olmayan kiracı e-belge kesemez; demo kiracıya GİB test VKN'si verilir.
        if (string.IsNullOrWhiteSpace(tenant.TaxNumber))
        {
            tenant.TaxNumber = "1234567801";
            tenant.TaxOffice = "Kadıköy";
            // Sağlık turizmi (334) senaryosunun demo/test edilebilmesi için yetki belgesi bayrağı açılır.
            tenant.HasHealthTourismAuthorization = true;
            await db.SaveChangesAsync(ct);
            logger.LogInformation("Demo kiracıya vergi kimliği tanımlandı. TenantId={TenantId}", tenantId);
        }

        // Klinik adres alanları UBL PostalAddress için zorunlu.
        var clinic = await db.Clinics.IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && !c.IsDeleted, ct);
        if (clinic is not null && string.IsNullOrWhiteSpace(clinic.City))
        {
            clinic.City = "İstanbul";
            clinic.District = "Kadıköy";
            clinic.Address ??= "Bağdat Caddesi No:100";
            clinic.Email ??= "info@demodis.local";
            await db.SaveChangesAsync(ct);
        }

        await SeedIntegrationSettingsAsync(sp, db, tenantId, logger, ct);
        await SeedTaxpayersAsync(db, logger, ct);
    }

    private static async Task SeedIntegrationSettingsAsync(
        IServiceProvider sp, AppDbContext db, long tenantId, ILogger logger, CancellationToken ct)
    {
        var exists = await db.TenantIntegrationSettings.IgnoreQueryFilters()
            .AnyAsync(s => s.TenantId == tenantId && s.IntegrationKey == "EInvoice" && !s.IsDeleted, ct);
        if (exists) return;

        var store = sp.GetRequiredService<IIntegrationSettingsStore>();
        var json = JsonSerializer.Serialize(new
        {
            username = UyumsoftTestUser,
            password = UyumsoftTestUser,
        });

        await store.UpsertAsync(tenantId, "EInvoice", "uyumsoft", "Test", json, isEnabled: true, ct);
        logger.LogInformation("Demo kiracıya Uyumsoft TEST e-belge ayarı tanımlandı. TenantId={TenantId}", tenantId);
    }

    /// <summary>
    /// GİB mükellef aynası örnekleri. Gerçek liste GibTaxpayerSyncJob ile entegratörden gelir;
    /// bu satırlar e-Fatura/e-Arşiv kararının seed'siz ortamda da denenebilmesi içindir.
    /// </summary>
    private static async Task SeedTaxpayersAsync(AppDbContext db, ILogger logger, CancellationToken ct)
    {
        if (await db.GibTaxpayers.AnyAsync(ct)) return;

        var now = DateTime.UtcNow;
        db.GibTaxpayers.AddRange(
            new GibTaxpayer
            {
                Vkn = "1234567801",
                Title = "Demo Diş Kliniği A.Ş.",
                Alias = "urn:mail:defaultpk@demodis.local",
                AccountType = "PK",
                FirstSeenUtc = now,
                LastSyncUtc = now,
            },
            new GibTaxpayer
            {
                Vkn = "9876543210",
                Title = "Anadolu Sağlık Hizmetleri Ltd. Şti.",
                Alias = "urn:mail:defaultpk@anadolusaglik.com.tr",
                AccountType = "PK",
                FirstSeenUtc = now,
                LastSyncUtc = now,
            });

        await db.SaveChangesAsync(ct);
        logger.LogInformation("GİB mükellef aynası örnek kayıtları yüklendi.");
    }

    /// <summary>
    /// Şahıs hekim (e-SMM) senaryosunun denenebilmesi için ikinci bir kiracı.
    /// Yalnız dev ortamında ve yalnız hiç yoksa açılır.
    /// </summary>
    public static async Task<long?> EnsureSoleProprietorTenantAsync(
        IServiceProvider sp, AppDbContext db, ILogger logger, CancellationToken ct = default)
    {
        const string email = "hekim@dental.local";
        var existing = await db.Users.IgnoreQueryFilters()
            .Where(u => u.NormalizedEmail == email.ToUpperInvariant())
            .Select(u => u.TenantId)
            .FirstOrDefaultAsync(ct);
        if (existing is { } found) return found;

        var provisioning = sp.GetRequiredService<Dental.Application.Tenants.ITenantProvisioningService>();
        var result = await provisioning.CreateAsync(new Dental.Application.Tenants.CreateTenantRequest(
            ClinicName: "Dt. Selin Aydın Muayenehanesi",
            LegalType: TenantLegalType.SoleProprietor,
            AdminEmail: email,
            AdminFirstName: "Selin",
            AdminLastName: "Aydın",
            AdminPassword: DbSeeder.DemoPassword,
            TaxNumber: "11111111110"), ct);

        var tenant = await db.Tenants.IgnoreQueryFilters().FirstAsync(t => t.Id == result.TenantId, ct);
        tenant.TaxOffice = "Beşiktaş";
        var clinic = await db.Clinics.IgnoreQueryFilters().FirstAsync(c => c.Id == result.ClinicId, ct);
        clinic.City = "İstanbul";
        clinic.District = "Beşiktaş";
        clinic.Address = "Barbaros Bulvarı No:12";
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Şahıs hekim (e-SMM) demo kiracısı oluşturuldu. TenantId={TenantId}", result.TenantId);
        return result.TenantId;
    }
}
