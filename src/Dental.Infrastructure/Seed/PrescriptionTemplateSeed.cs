using Dental.Domain.Entities;
using Dental.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Seed;

/// <summary>
/// Kiracı şablonu: hazır reçete şablonları ('Çekim Sonrası', 'Apse'). Kalemler merkezi ilaç
/// listesine barkodla bağlanır — DrugSeedTemplate'ten SONRA çalışmalıdır. Idempotent:
/// kiracıda herhangi bir reçete şablonu varsa dokunmaz (AnamnesisSeedTemplate deseni).
/// </summary>
public static class PrescriptionTemplateSeed
{
    private sealed record TemplateItem(string DrugBarcode, int BoxCount, string? Dose, string? Frequency, string? Duration, string? UsageNote);

    private sealed record Template(string Name, IReadOnlyList<TemplateItem> Items);

    // Barkodlar drugs.json'daki merkezi kalemler: amoksisilin 1000, parasetamol 500,
    // klorheksidin %0.12, amoksisilin+klavulanat 1000, metronidazol 500, ibuprofen 600.
    private static readonly IReadOnlyList<Template> Templates =
    [
        new("Çekim Sonrası",
        [
            new("8690000000029", 1, "1000 mg", "2x1", "5 gün", "Tok karnına"),
            new("8690000000272", 1, "500 mg", "3x1", "3 gün", "Ağrı oldukça"),
            new("8690000000494", 1, "%0.12", "2x1", "7 gün", "15 ml ile 30 sn çalkalayın; ilk 24 saat çalkalamayınız"),
        ]),
        new("Apse",
        [
            new("8690000000050", 1, "875/125 mg", "2x1", "7 gün", "Yemekle birlikte"),
            new("8690000000098", 1, "500 mg", "3x1", "7 gün", "Alkol almayınız"),
            new("8690000000326", 1, "600 mg", "2x1", "5 gün", "Tok karnına"),
        ]),
    ];

    /// <summary>Kiracıda hiç reçete şablonu yoksa hazır şablonları ekler. Eklenen şablon sayısını döner.</summary>
    public static async Task<int> ApplyToTenantAsync(AppDbContext db, long tenantId, CancellationToken ct = default)
    {
        var hasTemplate = await db.PrescriptionTemplates.IgnoreQueryFilters()
            .AnyAsync(t => t.TenantId == tenantId && !t.IsDeleted, ct);
        if (hasTemplate) return 0;

        var barcodes = Templates.SelectMany(t => t.Items).Select(i => i.DrugBarcode).Distinct().ToList();
        var drugIds = await db.Drugs
            .Where(d => d.TenantId == null && d.Barcode != null && barcodes.Contains(d.Barcode))
            .ToDictionaryAsync(d => d.Barcode!, d => d.Id, ct);
        if (drugIds.Count != barcodes.Count) return 0; // merkezi ilaç listesi henüz yüklenmemiş

        foreach (var template in Templates)
        {
            var entity = new PrescriptionTemplate { TenantId = tenantId, Name = template.Name };
            foreach (var item in template.Items)
            {
                entity.Items.Add(new PrescriptionTemplateItem
                {
                    TenantId = tenantId,
                    DrugId = drugIds[item.DrugBarcode],
                    BoxCount = item.BoxCount,
                    Dose = item.Dose,
                    Frequency = item.Frequency,
                    Duration = item.Duration,
                    UsageNote = item.UsageNote,
                });
            }
            db.PrescriptionTemplates.Add(entity);
        }
        await db.SaveChangesAsync(ct);
        return Templates.Count;
    }
}
