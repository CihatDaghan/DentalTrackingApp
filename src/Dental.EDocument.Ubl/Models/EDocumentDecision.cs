namespace Dental.EDocument.Ubl.Models;

/// <summary>Karar motoru girdisi — sağlayıcıdan ve domain'den bağımsız.</summary>
public sealed record EDocumentResolutionRequest
{
    public required SellerLegalType SellerLegalType { get; init; }

    /// <summary>(c)-4: 334 istisnası için tenant'ın sağlık turizmi yetki belgesi şartı.</summary>
    public bool TenantHasHealthTourismAuthorization { get; init; }

    public bool BuyerIsGibEInvoiceUser { get; init; }

    public bool BuyerIsGovernment { get; init; }

    public bool BuyerIsForeign { get; init; }

    /// <summary>(c)-3: e-SMM GV stopajı yalnız vergi mükellefi alıcıda.</summary>
    public bool BuyerIsVatRegistered { get; init; }

    public string? BuyerPassportNumber { get; init; }

    public string? BuyerNationality { get; init; }

    /// <summary>Türkiye'ye son giriş tarihi (yabancı hastada zorunlu).</summary>
    public DateOnly? BuyerLastEntryDate { get; init; }

    /// <summary>(c)-4: estetik (KDV %20) kalem var mı — 334 ile birleşemez.</summary>
    public bool HasAestheticLines { get; init; }

    public bool IsRefund { get; init; }

    public string? SourceInvoiceNumber { get; init; }

    public DateOnly? SourceIssueDate { get; init; }
}

/// <summary>Karar motoru çıktısı.</summary>
public sealed record EDocumentDecision
{
    public DocumentKind DocumentKind { get; init; }

    public string ProfileId { get; init; } = "";

    public string TypeCode { get; init; } = "";

    /// <summary>İstisnada 0 — satır KDV oranlarını ezer.</summary>
    public decimal? VatRateOverride { get; init; }

    public string? ExemptionCode { get; init; }

    public string? ExemptionReason { get; init; }

    public string? WithholdingCode { get; init; }

    /// <summary>Tevkifat oranı yüzde (5/10 → 50).</summary>
    public decimal? WithholdingPercent { get; init; }

    public bool AppliesGvStopaj { get; init; }

    public IReadOnlyList<string> Errors { get; init; } = [];

    public bool IsValid => Errors.Count == 0;
}
