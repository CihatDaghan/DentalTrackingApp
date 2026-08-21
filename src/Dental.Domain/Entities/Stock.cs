using Dental.Domain.Common;
using Dental.Domain.Enums;

namespace Dental.Domain.Entities;

/// <summary>Stok kategorisi (Sarf, Anestezi, Ölçü, Temizlik...).</summary>
public class StockCategory : TenantEntity
{
    public required string Name { get; set; }
}

/// <summary>
/// Stok kartı. CurrentQty denormalizedir ve YALNIZ StockService üzerinden hareketle aynı
/// transaction'da atomik UPDATE (LedgerService bakiye deseni) ile güncellenir.
/// CurrentQty &lt;= MinQty → düşük stok uyarısı (dashboard sayacı).
/// </summary>
public class StockItem : TenantEntity
{
    public long ClinicId { get; set; }
    public long CategoryId { get; set; }
    public required string Name { get; set; }
    public string? Barcode { get; set; }
    /// <summary>adet / kutu / ml / paket.</summary>
    public required string Unit { get; set; }
    /// <summary>Denormalize güncel miktar; hareketlerle atomik güncellenir.</summary>
    public decimal CurrentQty { get; set; }
    /// <summary>Kritik seviye — altına düşünce uyarı.</summary>
    public decimal MinQty { get; set; }
    public decimal? LastPurchasePrice { get; set; }
    public bool IsActive { get; set; } = true;

    public StockCategory? Category { get; set; }
}

/// <summary>
/// Stok hareketi. In/Out'ta Qty pozitif miktardır; Adjustment'ta harekete hesaplanan
/// işaretli fark yazılır (istekteki Qty = yeni mutlak değer).
/// </summary>
public class StockMovement : TenantEntity
{
    public long ClinicId { get; set; }
    public long StockItemId { get; set; }
    public StockMovementDirection Direction { get; set; }
    public decimal Qty { get; set; }
    public decimal? UnitCost { get; set; }
    public StockMovementRefType RefType { get; set; }
    public DateTime MovedAtUtc { get; set; }
    public string? Note { get; set; }
    public long? UserId { get; set; }
}
