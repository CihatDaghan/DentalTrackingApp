namespace Dental.EDocument.Ubl.Models;

/// <summary>
/// Belge toplamları. Hesaplama çağıran taraftadır; kütüphane yalnız XML'e yazar.
/// LineExtensionTotal = satır LineTotal toplamı (iskonto sonrası, KDV hariç).
/// </summary>
public sealed record DocumentTotals
{
    public required decimal LineExtensionTotal { get; init; }

    public decimal DiscountTotal { get; init; }

    public required decimal VatTotal { get; init; }

    /// <summary>Tevkif edilen KDV (TEVKIFAT belgesinde).</summary>
    public decimal WithholdingVatTotal { get; init; }

    /// <summary>GV stopajı (yalnız e-SMM, mükellef alıcıda).</summary>
    public decimal GvStopajTotal { get; init; }

    public required decimal TaxExclusiveAmount { get; init; }

    public required decimal TaxInclusiveAmount { get; init; }

    /// <summary>Ödenecek: TaxInclusive − tevkifat − stopaj.</summary>
    public required decimal PayableAmount { get; init; }
}

/// <summary>KDV tevkifatı bilgisi (kamu alıcı, kod 616 = 5/10).</summary>
public sealed record WithholdingInfo
{
    /// <summary>GİB tevkifat kodu (ör. "616").</summary>
    public required string Code { get; init; }

    /// <summary>Tevkifat oranı yüzde olarak (5/10 → 50).</summary>
    public required decimal Percent { get; init; }

    /// <summary>Tevkif edilen KDV tutarı.</summary>
    public required decimal Amount { get; init; }
}

/// <summary>KDV istisnası (ISTISNA tipinde zorunlu).</summary>
public sealed record ExemptionInfo
{
    /// <summary>GİB istisna kodu (sağlık turizmi: "334").</summary>
    public required string Code { get; init; }

    public required string Reason { get; init; }
}

/// <summary>İade belgesinde kaynak belge (BillingReference).</summary>
public sealed record SourceDocumentReference
{
    public required string InvoiceNumber { get; init; }

    public required DateOnly IssueDate { get; init; }
}

/// <summary>e-SMM gelir vergisi stopajı ((c)-3: yalnız vergi mükellefi alıcıda).</summary>
public sealed record GvStopajInfo
{
    public decimal Percent { get; init; } = 20m;

    public required decimal Amount { get; init; }
}
