using Dental.Domain.Enums;

namespace Dental.Application.Stock;

// ---- Kategoriler ----

public sealed record StockCategoryUpsertRequest(string Name);

public sealed record StockCategoryDto(long Id, string Name);

// ---- Kartlar ----

public sealed record StockItemUpsertRequest(
    long CategoryId,
    string Name,
    string Unit,
    string? Barcode = null,
    decimal MinQty = 0,
    bool IsActive = true,
    long? ClinicId = null);

public sealed record StockItemDto(
    long Id,
    long ClinicId,
    long CategoryId,
    string CategoryName,
    string Name,
    string? Barcode,
    string Unit,
    decimal CurrentQty,
    decimal MinQty,
    decimal? LastPurchasePrice,
    bool IsActive,
    bool IsLow);

// ---- Hareketler ----

/// <summary>
/// Hareket ekleme. In/Out'ta Qty pozitif miktardır; Adjustment'ta Qty yeni MUTLAK değerdir
/// (fark serviste hesaplanıp harekete yazılır, CurrentQty yeni değere eşitlenir).
/// </summary>
public sealed record StockMovementCreateRequest(
    StockMovementDirection Direction,
    decimal Qty,
    StockMovementRefType RefType,
    decimal? UnitCost = null,
    DateTime? MovedAtUtc = null,
    string? Note = null);

public sealed record StockMovementDto(
    long Id,
    long StockItemId,
    StockMovementDirection Direction,
    decimal Qty,
    decimal? UnitCost,
    StockMovementRefType RefType,
    DateTime MovedAtUtc,
    string? Note,
    long? UserId,
    string? UserName);
