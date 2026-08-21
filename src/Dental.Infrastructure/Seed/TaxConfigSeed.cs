using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Seed;

/// <summary>
/// Vergi kod listeleri (global, kiracısız). Oranlar/kodlar ASLA hardcode edilmez;
/// servisler bu tablodan okur. Idempotent: yalnız eksik (ConfigType, Code, ValidFrom) satırları ekler.
/// </summary>
public static class TaxConfigSeed
{
    /// <summary>KDV oranlarının yürürlük tarihi (10 Temmuz 2023 düzenlemesi).</summary>
    private static readonly DateOnly ValidFrom = new(2023, 7, 10);

    public static async Task<int> SeedAsync(AppDbContext db, CancellationToken ct = default)
    {
        var rows = new List<TaxConfig>
        {
            // ---- KDV oranları ----
            new()
            {
                ConfigType = TaxConfigType.VatRate,
                Code = "10",
                Description = "Sağlık hizmetleri KDV oranı (I sayılı liste)",
                Rate = 10m,
                ValidFrom = ValidFrom,
            },
            new()
            {
                ConfigType = TaxConfigType.VatRate,
                Code = "20",
                Description = "Genel KDV oranı — estetik/beyazlatma işlemleri",
                Rate = 20m,
                ValidFrom = ValidFrom,
            },
            new()
            {
                ConfigType = TaxConfigType.VatRate,
                Code = "0",
                Description = "İstisna kapsamı (KDV hesaplanmaz)",
                Rate = 0m,
                ValidFrom = ValidFrom,
            },

            // ---- İstisna kodları ----
            new()
            {
                ConfigType = TaxConfigType.ExemptionCode,
                Code = "334",
                Description =
                    "KDV Kanunu 13/l — Türkiye'de yerleşmiş olmayan yabancı uyruklu gerçek kişilere " +
                    "verilen sağlık hizmetlerinde istisna (sağlık turizmi)",
                ValidFrom = ValidFrom,
            },
            new()
            {
                ConfigType = TaxConfigType.ExemptionCode,
                Code = "301",
                Description = "KDV Kanunu 11/1-a — Mal ihracatı (hizmet ihracı senaryolarında referans)",
                ValidFrom = ValidFrom,
            },

            // ---- Tevkifat kodları ----
            new()
            {
                ConfigType = TaxConfigType.WithholdingCode,
                Code = "616",
                Description = "5018 sayılı Kanuna tabi kamu idarelerine verilen diğer hizmetler — 5/10 KDV tevkifatı",
                Rate = 50m,
                ValidFrom = ValidFrom,
            },

            // ---- Birim kodları ----
            new()
            {
                ConfigType = TaxConfigType.UnitCode,
                Code = "C62",
                Description = "Adet (UN/ECE Rec 20) — hizmet kalemlerinde varsayılan birim",
                ValidFrom = ValidFrom,
            },
        };

        var existing = await db.TaxConfigs.AsNoTracking()
            .Select(t => new { t.ConfigType, t.Code, t.ValidFrom })
            .ToListAsync(ct);
        var known = existing
            .Select(t => (t.ConfigType, t.Code, t.ValidFrom))
            .ToHashSet();

        var added = 0;
        foreach (var row in rows.Where(r => !known.Contains((r.ConfigType, r.Code, r.ValidFrom))))
        {
            db.TaxConfigs.Add(row);
            added++;
        }

        if (added > 0) await db.SaveChangesAsync(ct);
        return added;
    }
}
