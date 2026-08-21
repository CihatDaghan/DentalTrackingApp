namespace Dental.Domain.Enums;

/// <summary>
/// Stok hareket yönü. Adjustment (sayım düzeltmesi) isteğinde Qty yeni MUTLAK değerdir;
/// harekete hesaplanan fark (işaretli) yazılır ve CurrentQty yeni değere eşitlenir.
/// </summary>
public enum StockMovementDirection : byte
{
    In = 1,
    Out = 2,
    Adjustment = 3,
}

/// <summary>Stok hareketinin kaynağı.</summary>
public enum StockMovementRefType : byte
{
    Purchase = 1,
    TreatmentUse = 2,
    Waste = 3,
    /// <summary>Sayım düzeltmesi.</summary>
    Count = 4,
}
