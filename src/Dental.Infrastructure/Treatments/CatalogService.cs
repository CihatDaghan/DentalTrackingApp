using Dental.Application.Common;
using Dental.Application.Treatments;
using Dental.Domain.Entities;
using Dental.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Treatments;

public sealed class CatalogService(
    AppDbContext db,
    IValidator<TreatmentCategoryUpsertRequest> categoryValidator,
    IValidator<TreatmentDefinitionUpsertRequest> definitionValidator,
    IValidator<PriceListUpsertRequest> priceListValidator,
    IValidator<PriceListItemsSaveRequest> priceListItemsValidator) : ICatalogService
{
    // ---- Kategoriler ----

    public async Task<IReadOnlyList<TreatmentCategoryDto>> ListCategoriesAsync(CancellationToken ct = default)
    {
        return await db.TreatmentCategories.AsNoTracking()
            .OrderBy(c => c.SortOrder).ThenBy(c => c.Name)
            .Select(c => new TreatmentCategoryDto(c.Id, c.Name, c.NameEn, c.ColorHex, c.SortOrder,
                db.TreatmentDefinitions.Count(d => d.CategoryId == c.Id)))
            .ToListAsync(ct);
    }

    public async Task<TreatmentCategoryDto> CreateCategoryAsync(TreatmentCategoryUpsertRequest request, CancellationToken ct = default)
    {
        await categoryValidator.ValidateAndThrowAsync(request, ct);
        await EnsureCategoryNameFreeAsync(request.Name, excludeId: null, ct);

        var category = new TreatmentCategory
        {
            Name = request.Name.Trim(),
            NameEn = request.NameEn?.Trim(),
            ColorHex = request.ColorHex,
            SortOrder = request.SortOrder,
        };
        db.TreatmentCategories.Add(category);
        await db.SaveChangesAsync(ct);
        return new TreatmentCategoryDto(category.Id, category.Name, category.NameEn, category.ColorHex, category.SortOrder, 0);
    }

    public async Task<TreatmentCategoryDto> UpdateCategoryAsync(long id, TreatmentCategoryUpsertRequest request, CancellationToken ct = default)
    {
        await categoryValidator.ValidateAndThrowAsync(request, ct);
        var category = await db.TreatmentCategories.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new KeyNotFoundException("Kategori bulunamadı.");
        await EnsureCategoryNameFreeAsync(request.Name, excludeId: id, ct);

        category.Name = request.Name.Trim();
        category.NameEn = request.NameEn?.Trim();
        category.ColorHex = request.ColorHex;
        category.SortOrder = request.SortOrder;
        await db.SaveChangesAsync(ct);

        var count = await db.TreatmentDefinitions.CountAsync(d => d.CategoryId == id, ct);
        return new TreatmentCategoryDto(category.Id, category.Name, category.NameEn, category.ColorHex, category.SortOrder, count);
    }

    public async Task DeleteCategoryAsync(long id, CancellationToken ct = default)
    {
        var category = await db.TreatmentCategories.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new KeyNotFoundException("Kategori bulunamadı.");
        if (await db.TreatmentDefinitions.AnyAsync(d => d.CategoryId == id, ct))
            throw new InvalidOperationException("Tedavi tanımı olan kategori silinemez; önce tanımları taşıyın.");
        db.TreatmentCategories.Remove(category); // interceptor soft delete'e çevirir
        await db.SaveChangesAsync(ct);
    }

    private async Task EnsureCategoryNameFreeAsync(string name, long? excludeId, CancellationToken ct)
    {
        var trimmed = name.Trim();
        if (await db.TreatmentCategories.AnyAsync(c => c.Name == trimmed && (excludeId == null || c.Id != excludeId), ct))
            throw new InvalidOperationException($"'{trimmed}' adında bir kategori zaten var.");
    }

    // ---- Tedavi tanımları ----

    public async Task<PagedResult<TreatmentDefinitionDto>> ListDefinitionsAsync(TreatmentCatalogQuery query, CancellationToken ct = default)
    {
        var q = db.TreatmentDefinitions.AsNoTracking();

        if (query.CategoryId is { } categoryId)
            q = q.Where(d => d.CategoryId == categoryId);
        if (query.IsActive is { } isActive)
            q = q.Where(d => d.IsActive == isActive);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            q = q.Where(d => d.Name.Contains(term) || d.Code.Contains(term)
                || (d.NameEn != null && d.NameEn.Contains(term))
                || (d.SutCode != null && d.SutCode.Contains(term)));
        }

        var page = new PageRequest(query.Page, query.PageSize);
        var total = await q.CountAsync(ct);
        var items = await ProjectDefinitions(q.OrderBy(d => d.Code))
            .Skip(page.Skip).Take(page.EffectivePageSize)
            .ToListAsync(ct);
        return new PagedResult<TreatmentDefinitionDto>(items, Math.Max(query.Page, 1), page.EffectivePageSize, total);
    }

    public async Task<TreatmentDefinitionDto> GetDefinitionAsync(long id, CancellationToken ct = default)
        => await ProjectDefinitions(db.TreatmentDefinitions.AsNoTracking().Where(d => d.Id == id)).FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("Tedavi tanımı bulunamadı.");

    public async Task<TreatmentDefinitionDto> CreateDefinitionAsync(TreatmentDefinitionUpsertRequest request, CancellationToken ct = default)
    {
        await definitionValidator.ValidateAndThrowAsync(request, ct);
        await EnsureCategoryExistsAsync(request.CategoryId, ct);
        await EnsureCodeFreeAsync(request.Code, excludeId: null, ct);

        var definition = new TreatmentDefinition
        {
            CategoryId = request.CategoryId,
            Code = request.Code.Trim(),
            Name = request.Name.Trim(),
        };
        ApplyDefinitionScalars(definition, request);
        db.TreatmentDefinitions.Add(definition);
        await db.SaveChangesAsync(ct);
        return await GetDefinitionAsync(definition.Id, ct);
    }

    public async Task<TreatmentDefinitionDto> UpdateDefinitionAsync(long id, TreatmentDefinitionUpsertRequest request, CancellationToken ct = default)
    {
        await definitionValidator.ValidateAndThrowAsync(request, ct);
        var definition = await db.TreatmentDefinitions.FirstOrDefaultAsync(d => d.Id == id, ct)
            ?? throw new KeyNotFoundException("Tedavi tanımı bulunamadı.");
        await EnsureCategoryExistsAsync(request.CategoryId, ct);
        await EnsureCodeFreeAsync(request.Code, excludeId: id, ct);

        definition.CategoryId = request.CategoryId;
        definition.Code = request.Code.Trim();
        definition.Name = request.Name.Trim();
        ApplyDefinitionScalars(definition, request);
        await db.SaveChangesAsync(ct);
        return await GetDefinitionAsync(id, ct);
    }

    public async Task DeleteDefinitionAsync(long id, CancellationToken ct = default)
    {
        var definition = await db.TreatmentDefinitions.FirstOrDefaultAsync(d => d.Id == id, ct)
            ?? throw new KeyNotFoundException("Tedavi tanımı bulunamadı.");
        if (await db.TreatmentRecords.AnyAsync(t => t.TreatmentDefinitionId == id, ct))
            throw new InvalidOperationException("Tedavi kaydı olan tanım silinemez; pasife çekin (IsActive=false).");
        db.TreatmentDefinitions.Remove(definition);
        await db.SaveChangesAsync(ct);
    }

    private static void ApplyDefinitionScalars(TreatmentDefinition definition, TreatmentDefinitionUpsertRequest request)
    {
        definition.NameEn = request.NameEn?.Trim();
        definition.SutCode = request.SutCode?.Trim();
        definition.DefaultPrice = request.DefaultPrice;
        definition.VatRate = request.VatRate;
        definition.ToothScope = request.ToothScope;
        definition.RequiresSurface = request.RequiresSurface;
        definition.ToothStatusEffect = request.ToothStatusEffect;
        definition.DefaultDurationMinutes = request.DefaultDurationMinutes;
        definition.IsActive = request.IsActive;
    }

    private async Task EnsureCategoryExistsAsync(long categoryId, CancellationToken ct)
    {
        if (!await db.TreatmentCategories.AnyAsync(c => c.Id == categoryId, ct))
            throw new KeyNotFoundException("Kategori bulunamadı.");
    }

    private async Task EnsureCodeFreeAsync(string code, long? excludeId, CancellationToken ct)
    {
        var trimmed = code.Trim();
        if (await db.TreatmentDefinitions.AnyAsync(d => d.Code == trimmed && (excludeId == null || d.Id != excludeId), ct))
            throw new InvalidOperationException($"'{trimmed}' kodu başka bir tedavi tanımında kullanılıyor.");
    }

    private IQueryable<TreatmentDefinitionDto> ProjectDefinitions(IQueryable<TreatmentDefinition> source) =>
        source.Select(d => new TreatmentDefinitionDto(
            d.Id, d.CategoryId, d.Category!.Name, d.Category.ColorHex,
            d.Code, d.Name, d.NameEn, d.SutCode, d.DefaultPrice, d.VatRate,
            d.ToothScope, d.RequiresSurface, d.ToothStatusEffect, d.DefaultDurationMinutes, d.IsActive));

    // ---- Fiyat listeleri ----

    public async Task<IReadOnlyList<PriceListDto>> ListPriceListsAsync(CancellationToken ct = default)
    {
        return await db.PriceLists.AsNoTracking()
            .OrderByDescending(p => p.IsDefault).ThenBy(p => p.Name)
            .Select(p => new PriceListDto(p.Id, p.Name, p.CurrencyCode.Trim(), p.ValidFrom, p.IsDefault,
                p.Items.Count(i => !i.IsDeleted)))
            .ToListAsync(ct);
    }

    public async Task<PriceListDto> CreatePriceListAsync(PriceListUpsertRequest request, CancellationToken ct = default)
    {
        await priceListValidator.ValidateAndThrowAsync(request, ct);
        var priceList = new PriceList
        {
            Name = request.Name.Trim(),
            CurrencyCode = request.CurrencyCode.Trim().ToUpperInvariant(),
            ValidFrom = request.ValidFrom,
            IsDefault = request.IsDefault,
        };
        if (request.IsDefault) await ClearDefaultFlagAsync(excludeId: null, ct);
        db.PriceLists.Add(priceList);
        await db.SaveChangesAsync(ct);
        return new PriceListDto(priceList.Id, priceList.Name, priceList.CurrencyCode, priceList.ValidFrom, priceList.IsDefault, 0);
    }

    public async Task<PriceListDto> UpdatePriceListAsync(long id, PriceListUpsertRequest request, CancellationToken ct = default)
    {
        await priceListValidator.ValidateAndThrowAsync(request, ct);
        var priceList = await db.PriceLists.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new KeyNotFoundException("Fiyat listesi bulunamadı.");

        if (request.IsDefault && !priceList.IsDefault) await ClearDefaultFlagAsync(excludeId: id, ct);
        if (!request.IsDefault && priceList.IsDefault)
            throw new InvalidOperationException("Varsayılan tarife kaldırılamaz; başka bir tarifeyi varsayılan yapın.");

        priceList.Name = request.Name.Trim();
        priceList.CurrencyCode = request.CurrencyCode.Trim().ToUpperInvariant();
        priceList.ValidFrom = request.ValidFrom;
        priceList.IsDefault = request.IsDefault;
        await db.SaveChangesAsync(ct);

        var count = await db.PriceListItems.CountAsync(i => i.PriceListId == id, ct);
        return new PriceListDto(priceList.Id, priceList.Name, priceList.CurrencyCode.Trim(), priceList.ValidFrom, priceList.IsDefault, count);
    }

    public async Task DeletePriceListAsync(long id, CancellationToken ct = default)
    {
        var priceList = await db.PriceLists.Include(p => p.Items).FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw new KeyNotFoundException("Fiyat listesi bulunamadı.");
        if (priceList.IsDefault)
            throw new InvalidOperationException("Varsayılan tarife silinemez; önce başka bir tarifeyi varsayılan yapın.");
        db.PriceListItems.RemoveRange(priceList.Items);
        db.PriceLists.Remove(priceList);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<PriceListItemDto>> GetPriceListItemsAsync(long priceListId, CancellationToken ct = default)
    {
        await EnsurePriceListExistsAsync(priceListId, ct);
        return await db.PriceListItems.AsNoTracking()
            .Where(i => i.PriceListId == priceListId)
            .OrderBy(i => i.TreatmentDefinition!.Code)
            .Select(i => new PriceListItemDto(
                i.TreatmentDefinitionId, i.TreatmentDefinition!.Code, i.TreatmentDefinition.Name, i.Price))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<PriceListItemDto>> SavePriceListItemsAsync(
        long priceListId, PriceListItemsSaveRequest request, CancellationToken ct = default)
    {
        await priceListItemsValidator.ValidateAndThrowAsync(request, ct);
        await EnsurePriceListExistsAsync(priceListId, ct);

        var definitionIds = request.Items.Select(i => i.TreatmentDefinitionId).ToList();
        var known = await db.TreatmentDefinitions
            .Where(d => definitionIds.Contains(d.Id)).Select(d => d.Id).ToHashSetAsync(ct);
        var unknown = definitionIds.FirstOrDefault(id => !known.Contains(id));
        if (unknown != 0)
            throw new KeyNotFoundException($"Tedavi tanımı bulunamadı: {unknown}.");

        var existing = await db.PriceListItems
            .Where(i => i.PriceListId == priceListId && definitionIds.Contains(i.TreatmentDefinitionId))
            .ToDictionaryAsync(i => i.TreatmentDefinitionId, ct);

        foreach (var item in request.Items)
        {
            if (existing.TryGetValue(item.TreatmentDefinitionId, out var row))
                row.Price = item.Price;
            else
                db.PriceListItems.Add(new PriceListItem
                {
                    PriceListId = priceListId,
                    TreatmentDefinitionId = item.TreatmentDefinitionId,
                    Price = item.Price,
                });
        }
        await db.SaveChangesAsync(ct);
        return await GetPriceListItemsAsync(priceListId, ct);
    }

    public async Task<decimal> ResolvePriceAsync(long treatmentDefinitionId, long? priceListId = null, CancellationToken ct = default)
    {
        var effectiveListId = priceListId
            ?? await db.PriceLists.Where(p => p.IsDefault).Select(p => (long?)p.Id).FirstOrDefaultAsync(ct);

        if (effectiveListId is { } listId)
        {
            var itemPrice = await db.PriceListItems
                .Where(i => i.PriceListId == listId && i.TreatmentDefinitionId == treatmentDefinitionId)
                .Select(i => (decimal?)i.Price)
                .FirstOrDefaultAsync(ct);
            if (itemPrice is { } price) return price;
        }

        return await db.TreatmentDefinitions
                .Where(d => d.Id == treatmentDefinitionId).Select(d => (decimal?)d.DefaultPrice).FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("Tedavi tanımı bulunamadı.");
    }

    private async Task EnsurePriceListExistsAsync(long priceListId, CancellationToken ct)
    {
        if (!await db.PriceLists.AnyAsync(p => p.Id == priceListId, ct))
            throw new KeyNotFoundException("Fiyat listesi bulunamadı.");
    }

    private async Task ClearDefaultFlagAsync(long? excludeId, CancellationToken ct)
    {
        var defaults = await db.PriceLists
            .Where(p => p.IsDefault && (excludeId == null || p.Id != excludeId)).ToListAsync(ct);
        foreach (var p in defaults) p.IsDefault = false;
    }

    // ---- ICD-10 ----

    public async Task<IReadOnlyList<IcdCodeDto>> SearchIcdCodesAsync(string? search, CancellationToken ct = default)
    {
        var q = db.IcdCodes.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            q = q.Where(c => c.Code.StartsWith(term) || c.Name.Contains(term)
                || (c.NameEn != null && c.NameEn.Contains(term)));
        }
        return await q.OrderBy(c => c.Code).Take(50)
            .Select(c => new IcdCodeDto(c.Id, c.Code, c.Name, c.NameEn))
            .ToListAsync(ct);
    }
}
