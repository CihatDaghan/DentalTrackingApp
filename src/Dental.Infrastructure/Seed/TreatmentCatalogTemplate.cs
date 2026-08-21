using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Seed;

/// <summary>
/// Kiracı şablonu: TDB yapısındaki tedavi kataloğu (Seed/Data/treatment_catalog.json) ile
/// ICD-10 diş bloğu (icd10_dental.json). Yeni kiracı açılışında katalog + kategoriler +
/// varsayılan fiyat listesi kopyalanır; ICD kodları tenant'sız global tabloya bir kez yüklenir.
/// </summary>
public static class TreatmentCatalogTemplate
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public sealed record CatalogFile(string PriceListName, IReadOnlyList<CategoryTemplate> Categories);

    public sealed record CategoryTemplate(
        string Name, string? NameEn, string? ColorHex, int SortOrder, IReadOnlyList<TreatmentTemplate> Treatments);

    public sealed record TreatmentTemplate(
        string Code,
        string Name,
        string? NameEn,
        string? SutCode,
        decimal DefaultPrice,
        decimal VatRate,
        ToothScope ToothScope,
        bool RequiresSurface,
        ToothStatusEffect ToothStatusEffect,
        int? DurationMinutes);

    public sealed record IcdTemplate(string Code, string Name, string? NameEn);

    public static CatalogFile LoadCatalog() => Load<CatalogFile>("treatment_catalog.json");

    public static IReadOnlyList<IcdTemplate> LoadIcdCodes() => Load<IReadOnlyList<IcdTemplate>>("icd10_dental.json");

    private static T Load<T>(string fileName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = $"Dental.Infrastructure.Seed.Data.{fileName}";
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Gömülü seed dosyası bulunamadı: {resourceName}");
        return JsonSerializer.Deserialize<T>(stream, JsonOptions)
            ?? throw new InvalidOperationException($"Seed dosyası çözümlenemedi: {resourceName}");
    }

    /// <summary>
    /// Katalog şablonunu kiracıya kopyalar. Idempotent: kiracıda herhangi bir tedavi
    /// kategorisi varsa dokunmaz. SuperAdmin bağlamında çağrılmalıdır (TenantId elle atanır).
    /// </summary>
    public static async Task<int> ApplyToTenantAsync(AppDbContext db, long tenantId, CancellationToken ct = default)
    {
        var hasCatalog = await db.TreatmentCategories.IgnoreQueryFilters()
            .AnyAsync(c => c.TenantId == tenantId && !c.IsDeleted, ct);
        if (hasCatalog) return 0;

        var catalog = LoadCatalog();
        var priceList = new PriceList
        {
            TenantId = tenantId,
            Name = catalog.PriceListName,
            CurrencyCode = "TRY",
            IsDefault = true,
        };
        db.PriceLists.Add(priceList);

        var count = 0;
        foreach (var categoryTemplate in catalog.Categories)
        {
            var category = new TreatmentCategory
            {
                TenantId = tenantId,
                Name = categoryTemplate.Name,
                NameEn = categoryTemplate.NameEn,
                ColorHex = categoryTemplate.ColorHex,
                SortOrder = categoryTemplate.SortOrder,
            };
            db.TreatmentCategories.Add(category);

            foreach (var t in categoryTemplate.Treatments)
            {
                var definition = new TreatmentDefinition
                {
                    TenantId = tenantId,
                    Category = category,
                    Code = t.Code,
                    Name = t.Name,
                    NameEn = t.NameEn,
                    SutCode = t.SutCode,
                    DefaultPrice = t.DefaultPrice,
                    VatRate = t.VatRate,
                    ToothScope = t.ToothScope,
                    RequiresSurface = t.RequiresSurface,
                    ToothStatusEffect = t.ToothStatusEffect,
                    DefaultDurationMinutes = t.DurationMinutes,
                    IsActive = true,
                };
                db.TreatmentDefinitions.Add(definition);
                db.PriceListItems.Add(new PriceListItem
                {
                    TenantId = tenantId,
                    PriceList = priceList,
                    TreatmentDefinition = definition,
                    Price = t.DefaultPrice,
                });
                count++;
            }
        }

        await db.SaveChangesAsync(ct);
        return count;
    }

    /// <summary>ICD-10 K00–K14 kodlarını global tabloya yükler (Code üzerinden idempotent upsert).</summary>
    public static async Task SeedIcdCodesAsync(AppDbContext db, CancellationToken ct = default)
    {
        var existing = await db.IcdCodes.Select(c => c.Code).ToHashSetAsync(ct);
        foreach (var icd in LoadIcdCodes().Where(i => !existing.Contains(i.Code)))
            db.IcdCodes.Add(new IcdCode { Code = icd.Code, Name = icd.Name, NameEn = icd.NameEn });
        await db.SaveChangesAsync(ct);
    }
}
