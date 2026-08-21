namespace Dental.EDocument.Ubl.Models;

public sealed record AddressInfo
{
    public string? StreetName { get; init; }

    /// <summary>İlçe.</summary>
    public required string CitySubdivisionName { get; init; }

    /// <summary>İl.</summary>
    public required string CityName { get; init; }

    public string? PostalZone { get; init; }

    public string CountryName { get; init; } = "Türkiye";

    /// <summary>ISO 3166-1 alpha-2 (ör. "TR").</summary>
    public string? CountryCode { get; init; }
}

/// <summary>Satıcı (klinik / şahıs hekim).</summary>
public sealed record SellerInfo
{
    /// <summary>Unvan; şahıs hekimde "Ad Soyad".</summary>
    public required string Name { get; init; }

    /// <summary>Şahıs hekim için; doluysa UBL Party içine cac:Person yazılır.</summary>
    public string? FirstName { get; init; }

    public string? LastName { get; init; }

    /// <summary>VKN (10 hane) veya şahıs hekimde TCKN (11 hane).</summary>
    public required string TaxId { get; init; }

    public required string TaxOffice { get; init; }

    public required AddressInfo Address { get; init; }

    public string? Email { get; init; }

    public bool IsPerson => TaxId.Length == 11;
}

/// <summary>Alıcı (bireysel hasta / kurum / kamu).</summary>
public sealed record BuyerInfo
{
    public required BuyerKind Kind { get; init; }

    /// <summary>Kurum/kamu unvanı.</summary>
    public string? CorporateName { get; init; }

    /// <summary>Bireysel hastada zorunlu (cac:Person).</summary>
    public string? FirstName { get; init; }

    public string? LastName { get; init; }

    /// <summary>TCKN (11) / VKN (10); yabancı hastada "2222222222".</summary>
    public string? TaxId { get; init; }

    public string? TaxOffice { get; init; }

    /// <summary>(c)-3: e-SMM GV stopajı yalnız vergi mükellefi alıcıya uygulanır.</summary>
    public bool IsVatRegistered { get; init; }

    public bool IsGibEInvoiceUser { get; init; }

    public bool IsForeign { get; init; }

    /// <summary>Yabancı hastada zorunlu (334 istisnası).</summary>
    public string? PassportNumber { get; init; }

    /// <summary>Uyruk (yabancı hastada zorunlu).</summary>
    public string? Nationality { get; init; }

    /// <summary>Türkiye'ye son giriş tarihi (yabancı hastada zorunlu).</summary>
    public DateOnly? LastEntryDate { get; init; }

    public AddressInfo? Address { get; init; }

    public string? Email { get; init; }

    public string DisplayName =>
        !string.IsNullOrWhiteSpace(CorporateName)
            ? CorporateName
            : $"{FirstName} {LastName}".Trim();
}
