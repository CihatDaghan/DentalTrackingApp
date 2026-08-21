using Dental.Application.Authorization;
using Dental.Application.Tenants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Tenants;

/// <summary>
/// Yeni kiracı açılışı. SuperAdmin bağlamında çalışır; TenantId'ler elle atanır
/// (interceptor süper adminde farklı tenant'a yazmaya izin verir).
/// Sonraki aşamalarda şablon veriler (tedavi kataloğu, anamnez/onam şablonları) buradan kopyalanacak.
/// </summary>
public sealed class TenantProvisioningService(AppDbContext db, UserManager<AppUser> userManager) : ITenantProvisioningService
{
    public async Task<CreateTenantResult> CreateAsync(CreateTenantRequest request, CancellationToken ct = default)
    {
        var emailTaken = await db.Users.IgnoreQueryFilters()
            .AnyAsync(u => u.NormalizedEmail == userManager.NormalizeEmail(request.AdminEmail), ct);
        if (emailTaken)
            throw new InvalidOperationException("Bu e-posta adresi başka bir hesapta kayıtlı.");

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var tenant = new Tenant
        {
            Name = request.ClinicName,
            LegalType = request.LegalType,
            TaxNumber = request.TaxNumber,
            Status = TenantStatus.Trial,
            TrialEndsAtUtc = DateTime.UtcNow.AddDays(30),
            // I: yeni kiracı varsayılan olarak "Başlangıç" planında açılır; süper admin değiştirir.
            PlanCode = await db.Plans.Where(p => p.IsActive).OrderBy(p => p.SortOrder)
                .Select(p => p.Code).FirstOrDefaultAsync(ct),
        };
        db.Tenants.Add(tenant);
        await db.SaveChangesAsync(ct);

        var clinic = new Clinic
        {
            TenantId = tenant.Id,
            Name = request.ClinicName,
            Phone = request.Phone,
        };
        db.Clinics.Add(clinic);

        // Sistem rollerini kiracıya kopyala (tenant kendi matrisini özelleştirebilsin diye kopya, referans değil).
        var permissions = await db.Permissions.ToListAsync(ct);
        var ownerRole = default(Role);
        foreach (var (roleName, includes) in PermissionCatalog.SystemRoles)
        {
            if (roleName == "SuperAdmin") continue;
            var role = new Role { TenantId = tenant.Id, Name = roleName, IsSystem = true };
            db.Roles.Add(role);
            foreach (var p in permissions.Where(p => includes(p.Code)))
                db.RolePermissions.Add(new RolePermission { Role = role, Permission = p });
            if (roleName == "Owner") ownerRole = role;
        }
        await db.SaveChangesAsync(ct);

        // Şablon veriler: tedavi kataloğu + kategoriler + varsayılan fiyat listesi (TDB yapısı).
        await Seed.TreatmentCatalogTemplate.ApplyToTenantAsync(db, tenant.Id, ct);
        // Şablon veriler: varsayılan gider kategorileri.
        await Seed.ExpenseCategoryTemplate.ApplyToTenantAsync(db, tenant.Id, ct);
        // Şablon veriler: varsayılan anamnez formu + onam şablonları (E1).
        await Seed.AnamnesisSeedTemplate.ApplyToTenantAsync(db, tenant.Id, ct);
        await Seed.ConsentSeedTemplate.ApplyToTenantAsync(db, tenant.Id, ct);
        // Şablon veriler: hazır reçete şablonları (E2) — merkezi ilaç listesi seed'ine bağlıdır.
        await Seed.PrescriptionTemplateSeed.ApplyToTenantAsync(db, tenant.Id, ct);
        // Şablon veriler: mesaj şablonları + otomasyon kuralları (G).
        await Seed.MessagingSeedTemplate.ApplyToTenantAsync(db, tenant.Id, ct);

        var admin = new AppUser
        {
            TenantId = tenant.Id,
            UserName = request.AdminEmail,
            Email = request.AdminEmail,
            FirstName = request.AdminFirstName,
            LastName = request.AdminLastName,
            UserType = UserType.Owner,
            EmailConfirmed = true,
        };
        var result = await userManager.CreateAsync(admin, request.AdminPassword);
        if (!result.Succeeded)
            throw new InvalidOperationException("Yönetici kullanıcı oluşturulamadı: " +
                string.Join("; ", result.Errors.Select(e => e.Description)));

        db.UserClinics.Add(new UserClinic { UserId = admin.Id, ClinicId = clinic.Id, IsDefault = true });
        db.UserRoles.Add(new UserRole { UserId = admin.Id, RoleId = ownerRole!.Id });
        await db.SaveChangesAsync(ct);

        await tx.CommitAsync(ct);
        return new CreateTenantResult(tenant.Id, clinic.Id, admin.Id);
    }
}
