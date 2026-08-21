using Dental.Domain.Entities;
using Dental.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Seed;

/// <summary>
/// Abonelik planları (global). Idempotent: yalnız eksik kodlar eklenir, mevcut planların
/// fiyat/kota alanlarına DOKUNULMAZ (süper admin panelinden değiştirilmiş olabilir).
/// Örnek duyuru bilinçli olarak seed edilmez.
/// </summary>
public static class PlanSeed
{
    private static readonly Plan[] Defaults =
    [
        new() { Code = "starter", Name = "Başlangıç", MaxUsers = 3, MaxPatients = 1_000, MonthlySmsQuota = 250, StorageGb = 5, PriceMonthly = 1_490m, SortOrder = 10 },
        new() { Code = "pro", Name = "Profesyonel", MaxUsers = 10, MaxPatients = 10_000, MonthlySmsQuota = 1_500, StorageGb = 50, PriceMonthly = 3_490m, SortOrder = 20 },
        new() { Code = "enterprise", Name = "Kurumsal", MaxUsers = 100, MaxPatients = 100_000, MonthlySmsQuota = 10_000, StorageGb = 500, PriceMonthly = 8_990m, SortOrder = 30 },
    ];

    public static async Task<int> SeedAsync(AppDbContext db, CancellationToken ct = default)
    {
        var existing = await db.Plans.Select(p => p.Code).ToHashSetAsync(ct);
        var added = 0;
        foreach (var plan in Defaults.Where(p => !existing.Contains(p.Code)))
        {
            db.Plans.Add(new Plan
            {
                Code = plan.Code,
                Name = plan.Name,
                MaxUsers = plan.MaxUsers,
                MaxPatients = plan.MaxPatients,
                MonthlySmsQuota = plan.MonthlySmsQuota,
                StorageGb = plan.StorageGb,
                PriceMonthly = plan.PriceMonthly,
                SortOrder = plan.SortOrder,
                IsActive = true,
            });
            added++;
        }
        if (added > 0) await db.SaveChangesAsync(ct);

        // Planı olmayan mevcut kiracılar deneme sürümünde başlangıç planına bağlanır.
        var assigned = await db.Tenants.IgnoreQueryFilters()
            .Where(t => t.PlanCode == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.PlanCode, "starter"), ct);

        return added + assigned;
    }
}
