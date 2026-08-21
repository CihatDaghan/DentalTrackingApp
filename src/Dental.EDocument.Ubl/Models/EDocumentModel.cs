namespace Dental.EDocument.Ubl.Models;

/// <summary>
/// Sağlayıcıdan bağımsız e-belge modeli. Bu kütüphane numara/ETTN ÜRETMEZ:
/// (c)-1 gereği ikisi de Draft→UblGenerated geçişinde dışarıda atanır ve buraya hazır gelir.
/// </summary>
public sealed record EDocumentModel
{
    public required DocumentKind Kind { get; init; }

    /// <summary>TEMELFATURA / TICARIFATURA / EARSIVFATURA; e-SMM'de EARSIVBELGE (açık madde).</summary>
    public required string ProfileId { get; init; }

    /// <summary>SATIS / IADE / TEVKIFAT / ISTISNA (e-SMM'de InvoiceTypeCode olarak yazılmaz).</summary>
    public required string TypeCode { get; init; }

    /// <summary>
    /// e-SMM'de <c>cbc:CreditNoteTypeCode</c>. AÇIK MADDE: GİB e-SMM kılavuzu yayımlamadığı için
    /// doğru değer teyit edilemedi; kardeş CreditNote kılavuzları bu alanı tanımlıyor.
    /// Doluysa yazılır, boşsa hiç yazılmaz — entegratör teyidi geldiğinde tek satırla açılır.
    /// </summary>
    public string? CreditNoteTypeCode { get; init; }

    /// <summary>16 hane: 3 harf seri + 4 hane yıl + 9 hane sıra (dışarıdan atanır).</summary>
    public required string InvoiceNumber { get; init; }

    /// <summary>ETTN (UUID v4, dışarıdan atanır).</summary>
    public required Guid Ettn { get; init; }

    public required DateOnly IssueDate { get; init; }

    /// <summary>e-Arşiv'de zorunlu.</summary>
    public TimeOnly? IssueTime { get; init; }

    public string CurrencyCode { get; init; } = "TRY";

    /// <summary>Dövizli belgede TRY karşılığı kur (PricingExchangeRate).</summary>
    public decimal? ExchangeRate { get; init; }

    public IReadOnlyList<string> Notes { get; init; } = [];

    public required SellerInfo Seller { get; init; }

    public required BuyerInfo Buyer { get; init; }

    public required IReadOnlyList<DocumentLine> Lines { get; init; }

    public required DocumentTotals Totals { get; init; }

    /// <summary>TEVKIFAT tipinde zorunlu.</summary>
    public WithholdingInfo? Withholding { get; init; }

    /// <summary>ISTISNA tipinde zorunlu.</summary>
    public ExemptionInfo? Exemption { get; init; }

    /// <summary>IADE tipinde zorunlu.</summary>
    public SourceDocumentReference? SourceDocument { get; init; }

    /// <summary>e-SMM'de, alıcı vergi mükellefiyse.</summary>
    public GvStopajInfo? GvStopaj { get; init; }
}
