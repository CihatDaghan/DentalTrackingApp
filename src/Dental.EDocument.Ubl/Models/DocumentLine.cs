namespace Dental.EDocument.Ubl.Models;

/// <summary>Belge satırı (hizmet kalemi).</summary>
public sealed record DocumentLine
{
    /// <summary>Hizmet adı (ör. "İmplant uygulaması").</summary>
    public required string Name { get; init; }

    public required decimal Quantity { get; init; }

    /// <summary>UN/ECE birim kodu; hizmette "C62" (adet).</summary>
    public string UnitCode { get; init; } = "C62";

    public required decimal UnitPrice { get; init; }

    /// <summary>Satır iskontosu (tutar).</summary>
    public decimal DiscountAmount { get; init; }

    /// <summary>KDV oranı, yüzde (10, 20, istisnada 0).</summary>
    public required decimal VatRate { get; init; }

    public required decimal VatAmount { get; init; }

    /// <summary>İskonto sonrası, KDV hariç satır tutarı (LineExtensionAmount).</summary>
    public required decimal LineTotal { get; init; }

    /// <summary>(c)-4: estetik işlem (KDV %20) — 334 istisnasıyla birleşemez.</summary>
    public bool IsAesthetic { get; init; }
}
