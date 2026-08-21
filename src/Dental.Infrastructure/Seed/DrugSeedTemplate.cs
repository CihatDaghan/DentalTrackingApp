using System.Reflection;
using System.Text.Json;
using Dental.Domain.Entities;
using Dental.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Seed;

/// <summary>
/// Merkezi ilaç listesi (Seed/Data/drugs.json, ~90 kalem diş hekimliği ilacı).
/// Global tabloya TenantId NULL ile yüklenir; barkod üzerinden idempotent.
/// Kiracıya özel ilaçlar (TenantId dolu) bu seed'den etkilenmez.
/// </summary>
public static class DrugSeedTemplate
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public sealed record DrugTemplate(
        string Barcode,
        string Name,
        string? AtcCode,
        string? Form,
        string? DefaultDose,
        string? DefaultUsage,
        bool IsControlled);

    public static IReadOnlyList<DrugTemplate> Load()
    {
        var assembly = Assembly.GetExecutingAssembly();
        const string resourceName = "Dental.Infrastructure.Seed.Data.drugs.json";
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Gömülü seed dosyası bulunamadı: {resourceName}");
        return JsonSerializer.Deserialize<IReadOnlyList<DrugTemplate>>(stream, JsonOptions)
            ?? throw new InvalidOperationException($"Seed dosyası çözümlenemedi: {resourceName}");
    }

    /// <summary>Merkezi listeyi yükler (barkod üzerinden idempotent). Eklenen kalem sayısını döner.</summary>
    public static async Task<int> SeedAsync(AppDbContext db, CancellationToken ct = default)
    {
        var existing = await db.Drugs
            .Where(d => d.TenantId == null && d.Barcode != null)
            .Select(d => d.Barcode!)
            .ToHashSetAsync(ct);

        var count = 0;
        foreach (var t in Load().Where(t => !existing.Contains(t.Barcode)))
        {
            db.Drugs.Add(new Drug
            {
                TenantId = null,
                Barcode = t.Barcode,
                Name = t.Name,
                AtcCode = t.AtcCode,
                Form = t.Form,
                DefaultDose = t.DefaultDose,
                DefaultUsage = t.DefaultUsage,
                IsControlled = t.IsControlled,
            });
            count++;
        }
        await db.SaveChangesAsync(ct);
        return count;
    }
}
