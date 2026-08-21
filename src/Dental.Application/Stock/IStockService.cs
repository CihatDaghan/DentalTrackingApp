namespace Dental.Application.Stock;

public interface IStockService
{
    // ---- Kategoriler ----
    Task<IReadOnlyList<StockCategoryDto>> ListCategoriesAsync(CancellationToken ct = default);
    Task<StockCategoryDto> CreateCategoryAsync(StockCategoryUpsertRequest request, CancellationToken ct = default);
    Task<StockCategoryDto> UpdateCategoryAsync(long id, StockCategoryUpsertRequest request, CancellationToken ct = default);
    /// <summary>Kartı olan kategori silinemez.</summary>
    Task DeleteCategoryAsync(long id, CancellationToken ct = default);

    // ---- Kartlar ----
    Task<IReadOnlyList<StockItemDto>> ListItemsAsync(string? search, long? categoryId, bool includeInactive = false, CancellationToken ct = default);
    Task<StockItemDto> GetItemAsync(long id, CancellationToken ct = default);
    Task<StockItemDto> CreateItemAsync(StockItemUpsertRequest request, CancellationToken ct = default);
    Task<StockItemDto> UpdateItemAsync(long id, StockItemUpsertRequest request, CancellationToken ct = default);
    /// <summary>Hareketi olan kart silinemez (soft delete yerine pasife alınmalı).</summary>
    Task DeleteItemAsync(long id, CancellationToken ct = default);
    /// <summary>Düşük stok: aktif kartlarda CurrentQty &lt;= MinQty (dashboard sayacı).</summary>
    Task<IReadOnlyList<StockItemDto>> ListLowStockAsync(CancellationToken ct = default);

    // ---- Hareketler ----
    /// <summary>
    /// Hareket + CurrentQty güncellemesi aynı transaction'da atomiktir (LedgerService bakiye deseni).
    /// Out'ta yetersiz stok reddedilir; Adjustment'ta CurrentQty isteğin Qty değerine eşitlenir.
    /// Güncel kart DTO'sunu döner.
    /// </summary>
    Task<StockItemDto> AddMovementAsync(long stockItemId, StockMovementCreateRequest request, CancellationToken ct = default);
    Task<IReadOnlyList<StockMovementDto>> ListMovementsAsync(long stockItemId, CancellationToken ct = default);
}
