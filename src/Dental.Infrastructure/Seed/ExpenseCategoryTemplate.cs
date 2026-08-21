using Dental.Domain.Entities;
using Dental.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Seed;

/// <summary>
/// Kiracı şablonu: varsayılan gider kategorileri. Yeni kiracı açılışında kopyalanır;
/// mevcut kiracılara DbSeeder idempotent uygular (eksik adlar tamamlanır).
/// </summary>
public static class ExpenseCategoryTemplate
{
    public static readonly IReadOnlyList<string> DefaultCategories =
        ["Kira", "Maaş", "Malzeme", "Laboratuvar", "Fatura/Aidat", "Vergi", "Diğer"];

    /// <summary>
    /// Eksik varsayılan kategorileri kiracıya ekler (ada göre idempotent).
    /// SuperAdmin bağlamında çağrılmalıdır (TenantId elle atanır). Eklenen sayıyı döner.
    /// </summary>
    public static async Task<int> ApplyToTenantAsync(AppDbContext db, long tenantId, CancellationToken ct = default)
    {
        var existing = await db.ExpenseCategories.IgnoreQueryFilters()
            .Where(c => c.TenantId == tenantId && !c.IsDeleted)
            .Select(c => c.Name)
            .ToHashSetAsync(ct);

        var count = 0;
        foreach (var name in DefaultCategories.Where(n => !existing.Contains(n)))
        {
            db.ExpenseCategories.Add(new ExpenseCategory { TenantId = tenantId, Name = name });
            count++;
        }
        if (count > 0) await db.SaveChangesAsync(ct);
        return count;
    }
}
