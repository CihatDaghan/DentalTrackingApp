using Dental.Application.Common;

namespace Dental.Application.Treatments;

/// <summary>Tedavi kataloğu: kategori + tanım CRUD, fiyat listeleri ve fiyat çözümleme.</summary>
public interface ICatalogService
{
    Task<IReadOnlyList<TreatmentCategoryDto>> ListCategoriesAsync(CancellationToken ct = default);
    Task<TreatmentCategoryDto> CreateCategoryAsync(TreatmentCategoryUpsertRequest request, CancellationToken ct = default);
    Task<TreatmentCategoryDto> UpdateCategoryAsync(long id, TreatmentCategoryUpsertRequest request, CancellationToken ct = default);
    /// <summary>Kategori ancak tedavi tanımı yoksa silinebilir.</summary>
    Task DeleteCategoryAsync(long id, CancellationToken ct = default);

    Task<PagedResult<TreatmentDefinitionDto>> ListDefinitionsAsync(TreatmentCatalogQuery query, CancellationToken ct = default);
    Task<TreatmentDefinitionDto> GetDefinitionAsync(long id, CancellationToken ct = default);
    Task<TreatmentDefinitionDto> CreateDefinitionAsync(TreatmentDefinitionUpsertRequest request, CancellationToken ct = default);
    Task<TreatmentDefinitionDto> UpdateDefinitionAsync(long id, TreatmentDefinitionUpsertRequest request, CancellationToken ct = default);
    /// <summary>Tedavi kaydı olan tanım silinmez; IsActive=false ile pasife çekilir.</summary>
    Task DeleteDefinitionAsync(long id, CancellationToken ct = default);

    Task<IReadOnlyList<PriceListDto>> ListPriceListsAsync(CancellationToken ct = default);
    Task<PriceListDto> CreatePriceListAsync(PriceListUpsertRequest request, CancellationToken ct = default);
    /// <summary>IsDefault=true verilirse diğer listelerin varsayılanlığı kaldırılır (tek IsDefault).</summary>
    Task<PriceListDto> UpdatePriceListAsync(long id, PriceListUpsertRequest request, CancellationToken ct = default);
    Task DeletePriceListAsync(long id, CancellationToken ct = default);
    Task<IReadOnlyList<PriceListItemDto>> GetPriceListItemsAsync(long priceListId, CancellationToken ct = default);
    Task<IReadOnlyList<PriceListItemDto>> SavePriceListItemsAsync(long priceListId, PriceListItemsSaveRequest request, CancellationToken ct = default);

    /// <summary>
    /// Fiyat çözümleme: verilen (yoksa varsayılan) tarifede kalem varsa kalem fiyatı,
    /// yoksa tanımın DefaultPrice'ı.
    /// </summary>
    Task<decimal> ResolvePriceAsync(long treatmentDefinitionId, long? priceListId = null, CancellationToken ct = default);

    Task<IReadOnlyList<IcdCodeDto>> SearchIcdCodesAsync(string? search, CancellationToken ct = default);
}
