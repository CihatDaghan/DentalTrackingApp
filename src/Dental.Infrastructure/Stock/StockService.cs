using Dental.Application.Abstractions;
using Dental.Application.Stock;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Stock;

/// <summary>
/// Stok modülü. Denormalize StockItem.CurrentQty, hareket kaydı ile AYNI transaction'da
/// atomik <c>UPDATE ... SET CurrentQty = CurrentQty ± @qty</c> (satır kilidi) ile güncellenir
/// — LedgerService bakiye deseni. Out'ta yetersiz stok koşulu UPDATE'in WHERE'inde kontrol
/// edilir (yarış koşulunda eksiye düşmez). Adjustment optimistik döngüyle yeni mutlak değere eşitler.
/// </summary>
public sealed class StockService(
    AppDbContext db,
    ITenantContext tenant,
    IClock clock,
    IValidator<StockCategoryUpsertRequest> categoryValidator,
    IValidator<StockItemUpsertRequest> itemValidator,
    IValidator<StockMovementCreateRequest> movementValidator) : IStockService
{
    private const int AdjustmentRetryCount = 5;

    // ---- Kategoriler ----

    public async Task<IReadOnlyList<StockCategoryDto>> ListCategoriesAsync(CancellationToken ct = default) =>
        await db.StockCategories.AsNoTracking().OrderBy(c => c.Name)
            .Select(c => new StockCategoryDto(c.Id, c.Name))
            .ToListAsync(ct);

    public async Task<StockCategoryDto> CreateCategoryAsync(
        StockCategoryUpsertRequest request, CancellationToken ct = default)
    {
        await categoryValidator.ValidateAndThrowAsync(request, ct);
        var name = request.Name.Trim();
        if (await db.StockCategories.AnyAsync(c => c.Name == name, ct))
            throw new InvalidOperationException($"'{name}' adında bir stok kategorisi zaten var.");

        var category = new StockCategory { Name = name };
        db.StockCategories.Add(category);
        await db.SaveChangesAsync(ct);
        return new StockCategoryDto(category.Id, category.Name);
    }

    public async Task<StockCategoryDto> UpdateCategoryAsync(
        long id, StockCategoryUpsertRequest request, CancellationToken ct = default)
    {
        await categoryValidator.ValidateAndThrowAsync(request, ct);
        var category = await db.StockCategories.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new KeyNotFoundException("Stok kategorisi bulunamadı.");
        var name = request.Name.Trim();
        if (await db.StockCategories.AnyAsync(c => c.Name == name && c.Id != id, ct))
            throw new InvalidOperationException($"'{name}' adında bir stok kategorisi zaten var.");

        category.Name = name;
        await db.SaveChangesAsync(ct);
        return new StockCategoryDto(category.Id, category.Name);
    }

    public async Task DeleteCategoryAsync(long id, CancellationToken ct = default)
    {
        var category = await db.StockCategories.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new KeyNotFoundException("Stok kategorisi bulunamadı.");
        if (await db.StockItems.AnyAsync(i => i.CategoryId == id, ct))
            throw new InvalidOperationException("Kartı olan stok kategorisi silinemez.");

        db.StockCategories.Remove(category); // interceptor soft delete'e çevirir
        await db.SaveChangesAsync(ct);
    }

    // ---- Kartlar ----

    public async Task<IReadOnlyList<StockItemDto>> ListItemsAsync(
        string? search, long? categoryId, bool includeInactive = false, CancellationToken ct = default)
    {
        var q = db.StockItems.AsNoTracking().AsQueryable();
        if (!includeInactive) q = q.Where(i => i.IsActive);
        if (categoryId is { } cid) q = q.Where(i => i.CategoryId == cid);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            q = q.Where(i => i.Name.Contains(term) || (i.Barcode != null && i.Barcode.StartsWith(term)));
        }
        return await Project(q.OrderBy(i => i.Name)).ToListAsync(ct);
    }

    public async Task<StockItemDto> GetItemAsync(long id, CancellationToken ct = default) =>
        await Project(db.StockItems.AsNoTracking().Where(i => i.Id == id)).FirstOrDefaultAsync(ct)
            ?? throw new KeyNotFoundException("Stok kartı bulunamadı.");

    public async Task<StockItemDto> CreateItemAsync(
        StockItemUpsertRequest request, CancellationToken ct = default)
    {
        await itemValidator.ValidateAndThrowAsync(request, ct);
        await EnsureCategoryExistsAsync(request.CategoryId, ct);
        var name = request.Name.Trim();
        if (await db.StockItems.AnyAsync(i => i.Name == name, ct))
            throw new InvalidOperationException($"'{name}' adında bir stok kartı zaten var.");

        var item = new StockItem
        {
            ClinicId = await ResolveClinicIdAsync(request.ClinicId, ct),
            CategoryId = request.CategoryId,
            Name = name,
            Barcode = request.Barcode?.Trim(),
            Unit = request.Unit.Trim(),
            CurrentQty = 0, // açılış stoğu In hareketiyle girilir
            MinQty = request.MinQty,
            IsActive = request.IsActive,
        };
        db.StockItems.Add(item);
        await db.SaveChangesAsync(ct);
        return await GetItemAsync(item.Id, ct);
    }

    public async Task<StockItemDto> UpdateItemAsync(
        long id, StockItemUpsertRequest request, CancellationToken ct = default)
    {
        await itemValidator.ValidateAndThrowAsync(request, ct);
        var item = await db.StockItems.FirstOrDefaultAsync(i => i.Id == id, ct)
            ?? throw new KeyNotFoundException("Stok kartı bulunamadı.");
        await EnsureCategoryExistsAsync(request.CategoryId, ct);
        var name = request.Name.Trim();
        if (await db.StockItems.AnyAsync(i => i.Name == name && i.Id != id, ct))
            throw new InvalidOperationException($"'{name}' adında bir stok kartı zaten var.");

        item.CategoryId = request.CategoryId;
        item.Name = name;
        item.Barcode = request.Barcode?.Trim();
        item.Unit = request.Unit.Trim();
        item.MinQty = request.MinQty;
        item.IsActive = request.IsActive;
        if (request.ClinicId is { } clinicId) item.ClinicId = clinicId;
        await db.SaveChangesAsync(ct);
        return await GetItemAsync(id, ct);
    }

    public async Task DeleteItemAsync(long id, CancellationToken ct = default)
    {
        var item = await db.StockItems.FirstOrDefaultAsync(i => i.Id == id, ct)
            ?? throw new KeyNotFoundException("Stok kartı bulunamadı.");
        if (await db.StockMovements.AnyAsync(m => m.StockItemId == id, ct))
            throw new InvalidOperationException("Hareketi olan stok kartı silinemez; kartı pasife alın.");

        db.StockItems.Remove(item); // interceptor soft delete'e çevirir
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<StockItemDto>> ListLowStockAsync(CancellationToken ct = default) =>
        await Project(db.StockItems.AsNoTracking()
                .Where(i => i.IsActive && i.CurrentQty <= i.MinQty)
                .OrderBy(i => i.Name))
            .ToListAsync(ct);

    // ---- Hareketler ----

    public async Task<StockItemDto> AddMovementAsync(
        long stockItemId, StockMovementCreateRequest request, CancellationToken ct = default)
    {
        await movementValidator.ValidateAndThrowAsync(request, ct);
        var item = await db.StockItems.AsNoTracking().FirstOrDefaultAsync(i => i.Id == stockItemId, ct)
            ?? throw new KeyNotFoundException("Stok kartı bulunamadı.");

        // Çağıran transaction başlatmışsa ona katıl; yoksa kendi transaction'ımızı aç (LedgerService deseni).
        var ownsTransaction = db.Database.CurrentTransaction is null;
        var tx = ownsTransaction ? await db.Database.BeginTransactionAsync(ct) : null;
        try
        {
            var movement = new StockMovement
            {
                ClinicId = item.ClinicId,
                StockItemId = stockItemId,
                Direction = request.Direction,
                Qty = request.Qty,
                UnitCost = request.UnitCost,
                RefType = request.RefType,
                MovedAtUtc = request.MovedAtUtc ?? clock.UtcNow,
                Note = request.Note,
                UserId = tenant.UserId,
            };

            switch (request.Direction)
            {
                case StockMovementDirection.In:
                {
                    db.StockMovements.Add(movement);
                    await db.SaveChangesAsync(ct);
                    var qty = request.Qty;
                    if (request is { RefType: StockMovementRefType.Purchase, UnitCost: { } unitCost })
                    {
                        await db.StockItems.Where(i => i.Id == stockItemId)
                            .ExecuteUpdateAsync(s => s
                                .SetProperty(i => i.CurrentQty, i => i.CurrentQty + qty)
                                .SetProperty(i => i.LastPurchasePrice, unitCost), ct);
                    }
                    else
                    {
                        await db.StockItems.Where(i => i.Id == stockItemId)
                            .ExecuteUpdateAsync(s => s
                                .SetProperty(i => i.CurrentQty, i => i.CurrentQty + qty), ct);
                    }
                    break;
                }
                case StockMovementDirection.Out:
                {
                    db.StockMovements.Add(movement);
                    await db.SaveChangesAsync(ct);
                    var qty = request.Qty;
                    // Yetersiz stok koşulu UPDATE'in WHERE'inde: yarışta iki paralel çıkış eksiye düşüremez.
                    var affected = await db.StockItems
                        .Where(i => i.Id == stockItemId && i.CurrentQty >= qty)
                        .ExecuteUpdateAsync(s => s.SetProperty(i => i.CurrentQty, i => i.CurrentQty - qty), ct);
                    if (affected == 0)
                        throw new InvalidOperationException("Yetersiz stok: çıkış miktarı mevcut miktarı aşıyor.");
                    break;
                }
                case StockMovementDirection.Adjustment:
                {
                    // Qty = yeni mutlak değer; harekete hesaplanan işaretli fark yazılır.
                    var newQty = request.Qty;
                    var applied = false;
                    for (var attempt = 0; attempt < AdjustmentRetryCount && !applied; attempt++)
                    {
                        var current = await db.StockItems.AsNoTracking()
                            .Where(i => i.Id == stockItemId).Select(i => i.CurrentQty).FirstAsync(ct);
                        movement.Qty = newQty - current; // fark (işaretli)
                        applied = await db.StockItems
                            .Where(i => i.Id == stockItemId && i.CurrentQty == current)
                            .ExecuteUpdateAsync(s => s.SetProperty(i => i.CurrentQty, newQty), ct) > 0;
                    }
                    if (!applied)
                        throw new DbUpdateConcurrencyException(
                            "Stok sayımı eşzamanlı hareketler nedeniyle uygulanamadı; tekrar deneyin.");
                    db.StockMovements.Add(movement);
                    await db.SaveChangesAsync(ct);
                    break;
                }
                default:
                    throw new InvalidOperationException("Geçersiz hareket yönü.");
            }

            if (tx is not null) await tx.CommitAsync(ct);
        }
        finally
        {
            if (tx is not null) await tx.DisposeAsync();
        }

        return await GetItemAsync(stockItemId, ct);
    }

    public async Task<IReadOnlyList<StockMovementDto>> ListMovementsAsync(
        long stockItemId, CancellationToken ct = default)
    {
        if (!await db.StockItems.AnyAsync(i => i.Id == stockItemId, ct))
            throw new KeyNotFoundException("Stok kartı bulunamadı.");
        return await (
            from m in db.StockMovements.AsNoTracking()
            where m.StockItemId == stockItemId
            join u in db.Users on m.UserId equals (long?)u.Id into uj
            from u in uj.DefaultIfEmpty()
            orderby m.MovedAtUtc descending, m.Id descending
            select new StockMovementDto(m.Id, m.StockItemId, m.Direction, m.Qty, m.UnitCost, m.RefType,
                m.MovedAtUtc, m.Note, m.UserId, u != null ? u.FirstName + " " + u.LastName : null))
            .ToListAsync(ct);
    }

    // ---- Yardımcılar ----

    private async Task EnsureCategoryExistsAsync(long categoryId, CancellationToken ct)
    {
        if (!await db.StockCategories.AnyAsync(c => c.Id == categoryId, ct))
            throw new KeyNotFoundException("Stok kategorisi bulunamadı.");
    }

    private async Task<long> ResolveClinicIdAsync(long? requestClinicId, CancellationToken ct)
    {
        if (requestClinicId is { } explicitClinic) return explicitClinic;
        if (tenant.ClinicId is { } contextClinic) return contextClinic;
        return await db.Clinics.OrderBy(c => c.Id).Select(c => (long?)c.Id).FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException("Kiracının kliniği yok; stok kartı için klinik gereklidir.");
    }

    private IQueryable<StockItemDto> Project(IQueryable<StockItem> source) =>
        source.Select(i => new StockItemDto(
            i.Id, i.ClinicId, i.CategoryId, i.Category!.Name, i.Name, i.Barcode, i.Unit,
            i.CurrentQty, i.MinQty, i.LastPurchasePrice, i.IsActive, i.CurrentQty <= i.MinQty));
}
