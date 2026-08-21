using Dental.Application.Abstractions;
using Dental.Application.Authorization;
using Dental.Application.Tenants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dental.Infrastructure.Seed;

/// <summary>
/// Idempotent seed zinciri. Dev ortamında startup'ta çalışır.
/// 1) İzin kataloğu, 2) süper admin, 3) demo kiracı (yalnız hiç kiracı yoksa).
/// </summary>
public static class DbSeeder
{
    public const string SuperAdminEmail = "admin@dental.local";
    public const string SuperAdminPassword = "Admin!2026";
    public const string DemoEmail = "demo@dental.local";
    public const string DemoPassword = "Demo!2026";

    public static async Task SeedAsync(IServiceProvider services, CancellationToken ct = default)
    {
        // Seed süper admin bağlamında koşar: tenant filtresi atlanır, tenant damgalaması elle yapılır.
        using var scope = services.CreateScope();
        var setter = (ITenantContextSetter)scope.ServiceProvider.GetRequiredService<ITenantContext>();
        setter.Set(tenantId: null, clinicId: null, userId: null, isSuperAdmin: true);

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder");

        await SeedPermissionsAsync(db, ct);
        // I: abonelik planları (global) — kiracı açılışından ÖNCE hazır olmalı.
        var planCount = await PlanSeed.SeedAsync(db, ct);
        if (planCount > 0)
            logger.LogInformation("Abonelik planları yüklendi/eşlendi: Kayıt={Count}", planCount);
        await TreatmentCatalogTemplate.SeedIcdCodesAsync(db, ct);
        // Vergi kod listeleri (KDV/istisna/tevkifat/birim) — fatura servisleri buradan okur.
        var taxConfigCount = await TaxConfigSeed.SeedAsync(db, ct);
        if (taxConfigCount > 0)
            logger.LogInformation("Vergi kod listeleri yüklendi: Kalem={Count}", taxConfigCount);
        // Merkezi ilaç listesi kiracı şablonlarından (reçete şablonu) ÖNCE yüklenmelidir.
        var drugCount = await DrugSeedTemplate.SeedAsync(db, ct);
        if (drugCount > 0)
            logger.LogInformation("Merkezi ilaç listesi yüklendi: Kalem={Count}", drugCount);
        await SeedSuperAdminAsync(scope.ServiceProvider, db, ct);
        await SeedDemoTenantAsync(scope.ServiceProvider, db, logger, ct);
        await SeedTenantCatalogsAsync(db, logger, ct);
        await DemoDataSeeder.ApplyAsync(scope.ServiceProvider, db, logger, ct);
        await DemoClinicalSeeder.ApplyAsync(db, logger, ct);
        // F: demo kiracının vergi kimliği + Uyumsoft TEST ayarı + mükellef aynası örnekleri.
        await EInvoiceSeed.ApplyAsync(scope.ServiceProvider, db, logger, ct);
    }

    /// <summary>
    /// Şablon verileri olmayan mevcut kiracılara (örn. ilgili aşamadan önce açılmış kiracılar)
    /// tedavi kataloğu + gider kategorisi şablonlarını idempotent uygular. Yeni kiracılar provisioning'de alır.
    /// </summary>
    private static async Task SeedTenantCatalogsAsync(AppDbContext db, ILogger logger, CancellationToken ct)
    {
        var tenantIds = await db.Tenants.IgnoreQueryFilters().Select(t => t.Id).ToListAsync(ct);
        foreach (var tenantId in tenantIds)
        {
            var count = await TreatmentCatalogTemplate.ApplyToTenantAsync(db, tenantId, ct);
            if (count > 0)
                logger.LogInformation("Tedavi kataloğu yüklendi: TenantId={TenantId}, Kalem={Count}", tenantId, count);

            var expenseCategoryCount = await ExpenseCategoryTemplate.ApplyToTenantAsync(db, tenantId, ct);
            if (expenseCategoryCount > 0)
                logger.LogInformation("Gider kategorileri yüklendi: TenantId={TenantId}, Kalem={Count}",
                    tenantId, expenseCategoryCount);

            var anamnesisCount = await AnamnesisSeedTemplate.ApplyToTenantAsync(db, tenantId, ct);
            if (anamnesisCount > 0)
                logger.LogInformation("Anamnez şablonu yüklendi: TenantId={TenantId}, Soru={Count}",
                    tenantId, anamnesisCount);

            var consentCount = await ConsentSeedTemplate.ApplyToTenantAsync(db, tenantId, ct);
            if (consentCount > 0)
                logger.LogInformation("Onam şablonları yüklendi: TenantId={TenantId}, Şablon={Count}",
                    tenantId, consentCount);

            var prescriptionTemplateCount = await PrescriptionTemplateSeed.ApplyToTenantAsync(db, tenantId, ct);
            if (prescriptionTemplateCount > 0)
                logger.LogInformation("Reçete şablonları yüklendi: TenantId={TenantId}, Şablon={Count}",
                    tenantId, prescriptionTemplateCount);

            var messagingCount = await MessagingSeedTemplate.ApplyToTenantAsync(db, tenantId, ct);
            if (messagingCount > 0)
                logger.LogInformation("Mesaj şablonu ve otomasyon kuralları yüklendi: TenantId={TenantId}, Kayıt={Count}",
                    tenantId, messagingCount);
        }
    }

    private static async Task SeedPermissionsAsync(AppDbContext db, CancellationToken ct)
    {
        var existing = await db.Permissions.Select(p => p.Code).ToHashSetAsync(ct);
        foreach (var (code, module) in PermissionCatalog.All.Where(p => !existing.Contains(p.Code)))
            db.Permissions.Add(new Permission { Code = code, Module = module });
        await db.SaveChangesAsync(ct);
    }

    private static async Task SeedSuperAdminAsync(IServiceProvider sp, AppDbContext db, CancellationToken ct)
    {
        var userManager = sp.GetRequiredService<Microsoft.AspNetCore.Identity.UserManager<AppUser>>();
        var exists = await db.Users.IgnoreQueryFilters().AnyAsync(u => u.TenantId == null, ct);
        if (exists) return;

        var admin = new AppUser
        {
            TenantId = null,
            UserName = SuperAdminEmail,
            Email = SuperAdminEmail,
            FirstName = "Platform",
            LastName = "Admin",
            UserType = UserType.Owner,
            EmailConfirmed = true,
        };
        var result = await userManager.CreateAsync(admin, SuperAdminPassword);
        if (!result.Succeeded)
            throw new InvalidOperationException("Süper admin seed başarısız: " +
                string.Join("; ", result.Errors.Select(e => e.Description)));
    }

    private static async Task SeedDemoTenantAsync(IServiceProvider sp, AppDbContext db, ILogger logger, CancellationToken ct)
    {
        if (await db.Tenants.IgnoreQueryFilters().AnyAsync(ct)) return;

        var provisioning = sp.GetRequiredService<ITenantProvisioningService>();
        var result = await provisioning.CreateAsync(new CreateTenantRequest(
            ClinicName: "Demo Diş Kliniği",
            LegalType: TenantLegalType.Company,
            AdminEmail: DemoEmail,
            AdminFirstName: "Deniz",
            AdminLastName: "Yönetici",
            AdminPassword: DemoPassword), ct);
        logger.LogInformation("Demo kiracı oluşturuldu: TenantId={TenantId}", result.TenantId);
    }
}
