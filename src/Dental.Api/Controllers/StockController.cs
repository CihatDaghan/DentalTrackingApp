using Dental.Api.Auth;
using Dental.Application.Stock;
using Microsoft.AspNetCore.Mvc;

namespace Dental.Api.Controllers;

[ApiController]
[Route("api/v1")]
public sealed class StockController(IStockService stock) : ControllerBase
{
    // ---- Kategoriler ----

    [HttpGet("stock-categories")]
    [HasPermission("stock.read")]
    public async Task<ActionResult<IReadOnlyList<StockCategoryDto>>> ListCategories(CancellationToken ct)
        => Ok(await stock.ListCategoriesAsync(ct));

    [HttpPost("stock-categories")]
    [HasPermission("stock.create")]
    public async Task<ActionResult<StockCategoryDto>> CreateCategory(
        StockCategoryUpsertRequest request, CancellationToken ct)
        => Ok(await stock.CreateCategoryAsync(request, ct));

    [HttpPut("stock-categories/{id:long}")]
    [HasPermission("stock.update")]
    public async Task<ActionResult<StockCategoryDto>> UpdateCategory(
        long id, StockCategoryUpsertRequest request, CancellationToken ct)
        => Ok(await stock.UpdateCategoryAsync(id, request, ct));

    /// <summary>Kartı olan kategori silinemez.</summary>
    [HttpDelete("stock-categories/{id:long}")]
    [HasPermission("stock.delete")]
    public async Task<IActionResult> DeleteCategory(long id, CancellationToken ct)
    {
        await stock.DeleteCategoryAsync(id, ct);
        return NoContent();
    }

    // ---- Kartlar ----

    [HttpGet("stock-items")]
    [HasPermission("stock.read")]
    public async Task<ActionResult<IReadOnlyList<StockItemDto>>> ListItems(
        [FromQuery] string? search, [FromQuery] long? categoryId,
        [FromQuery] bool includeInactive = false, CancellationToken ct = default)
        => Ok(await stock.ListItemsAsync(search, categoryId, includeInactive, ct));

    /// <summary>Düşük stok listesi: aktif kartlarda CurrentQty &lt;= MinQty (dashboard sayacı).</summary>
    [HttpGet("stock-items/low")]
    [HasPermission("stock.read")]
    public async Task<ActionResult<IReadOnlyList<StockItemDto>>> ListLow(CancellationToken ct)
        => Ok(await stock.ListLowStockAsync(ct));

    [HttpGet("stock-items/{id:long}")]
    [HasPermission("stock.read")]
    public async Task<ActionResult<StockItemDto>> GetItem(long id, CancellationToken ct)
        => Ok(await stock.GetItemAsync(id, ct));

    [HttpPost("stock-items")]
    [HasPermission("stock.create")]
    public async Task<ActionResult<StockItemDto>> CreateItem(
        StockItemUpsertRequest request, CancellationToken ct)
    {
        var dto = await stock.CreateItemAsync(request, ct);
        return CreatedAtAction(nameof(GetItem), new { id = dto.Id }, dto);
    }

    [HttpPut("stock-items/{id:long}")]
    [HasPermission("stock.update")]
    public async Task<ActionResult<StockItemDto>> UpdateItem(
        long id, StockItemUpsertRequest request, CancellationToken ct)
        => Ok(await stock.UpdateItemAsync(id, request, ct));

    /// <summary>Hareketi olan kart silinemez; pasife alınmalı.</summary>
    [HttpDelete("stock-items/{id:long}")]
    [HasPermission("stock.delete")]
    public async Task<IActionResult> DeleteItem(long id, CancellationToken ct)
    {
        await stock.DeleteItemAsync(id, ct);
        return NoContent();
    }

    // ---- Hareketler ----

    /// <summary>
    /// Hareket ekler; CurrentQty aynı transaction'da atomik güncellenir.
    /// Adjustment'ta Qty = yeni mutlak değer. Güncel kart DTO'sunu döner.
    /// </summary>
    [HttpPost("stock-items/{id:long}/movements")]
    [HasPermission("stock.update")]
    public async Task<ActionResult<StockItemDto>> AddMovement(
        long id, StockMovementCreateRequest request, CancellationToken ct)
        => Ok(await stock.AddMovementAsync(id, request, ct));

    [HttpGet("stock-items/{id:long}/movements")]
    [HasPermission("stock.read")]
    public async Task<ActionResult<IReadOnlyList<StockMovementDto>>> ListMovements(long id, CancellationToken ct)
        => Ok(await stock.ListMovementsAsync(id, ct));
}
