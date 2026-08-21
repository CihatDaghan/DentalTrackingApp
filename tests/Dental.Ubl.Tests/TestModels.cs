using Dental.EDocument.Ubl;
using Dental.EDocument.Ubl.Models;

namespace Dental.Ubl.Tests;

/// <summary>Testlerde kullanılan örnek belge modelleri (toplamlar elle tutarlı hesaplanmıştır).</summary>
internal static class TestModels
{
    public static readonly Guid Ettn = Guid.Parse("f47ac10b-58cc-4372-a567-0e02b2c3d479");

    public static SellerInfo CompanySeller() => new()
    {
        Name = "Dent Klinik Ağız ve Diş Sağlığı Hizmetleri Ltd. Şti.",
        TaxId = "1234567890",
        TaxOffice = "Kadıköy",
        Address = new AddressInfo
        {
            StreetName = "Bağdat Cad. No:1",
            CitySubdivisionName = "Kadıköy",
            CityName = "İstanbul",
            PostalZone = "34710",
            CountryCode = "TR",
        },
        Email = "fatura@dentklinik.example",
    };

    public static SellerInfo SoleProprietorSeller() => new()
    {
        Name = "Ayşe Yılmaz",
        FirstName = "Ayşe",
        LastName = "Yılmaz",
        TaxId = "12345678901",
        TaxOffice = "Kadıköy",
        Address = new AddressInfo
        {
            CitySubdivisionName = "Kadıköy",
            CityName = "İstanbul",
        },
    };

    public static BuyerInfo IndividualBuyer() => new()
    {
        Kind = BuyerKind.IndividualPatient,
        FirstName = "Mehmet",
        LastName = "Demir",
        TaxId = "98765432109",
        Address = new AddressInfo { CitySubdivisionName = "Üsküdar", CityName = "İstanbul" },
        Email = "mehmet@example.com",
    };

    public static BuyerInfo CorporateBuyer(bool gibUser = true) => new()
    {
        Kind = BuyerKind.Corporate,
        CorporateName = "Sigorta A.Ş.",
        TaxId = "9876543210",
        TaxOffice = "Beşiktaş",
        IsVatRegistered = true,
        IsGibEInvoiceUser = gibUser,
        Address = new AddressInfo { CitySubdivisionName = "Beşiktaş", CityName = "İstanbul" },
    };

    public static BuyerInfo GovernmentBuyer() => new()
    {
        Kind = BuyerKind.Government,
        CorporateName = "İl Sağlık Müdürlüğü",
        TaxId = "1112223334",
        TaxOffice = "Fatih",
        IsVatRegistered = true,
        IsGibEInvoiceUser = true,
        Address = new AddressInfo { CitySubdivisionName = "Fatih", CityName = "İstanbul" },
    };

    public static BuyerInfo ForeignBuyer() => new()
    {
        Kind = BuyerKind.IndividualPatient,
        FirstName = "John",
        LastName = "Smith",
        // GİB: TCKN'si olmayan yabancı GERÇEK KİŞİ → 11 adet 1, schemeID="TCKN"
        // (10 haneli "2222222222" yabancı TÜZEL kişi içindir).
        TaxId = "11111111111",
        IsForeign = true,
        PassportNumber = "P1234567",
        Nationality = "GB", // ISO 3166-1 alpha-2 (schematron $CountryCodeList)
        LastEntryDate = new DateOnly(2026, 8, 1),
        Address = new AddressInfo
        {
            CitySubdivisionName = "Westminster",
            CityName = "London",
            CountryName = "Birleşik Krallık",
            CountryCode = "GB",
        },
    };

    /// <summary>2 satır: 2×5000 (iskonto 500) + 1×1000, KDV %10.</summary>
    public static IReadOnlyList<DocumentLine> StandardLines(decimal vatRate = 10m)
    {
        var factor = vatRate / 100m;
        return
        [
            new DocumentLine
            {
                Name = "İmplant uygulaması",
                Quantity = 2m,
                UnitPrice = 5000m,
                DiscountAmount = 500m,
                VatRate = vatRate,
                VatAmount = 9500m * factor,
                LineTotal = 9500m,
            },
            new DocumentLine
            {
                Name = "Kompozit dolgu",
                Quantity = 1m,
                UnitPrice = 1000m,
                VatRate = vatRate,
                VatAmount = 1000m * factor,
                LineTotal = 1000m,
            },
        ];
    }

    public static DocumentTotals StandardTotals() => new()
    {
        LineExtensionTotal = 10500m,
        DiscountTotal = 500m,
        VatTotal = 1050m,
        TaxExclusiveAmount = 10500m,
        TaxInclusiveAmount = 11550m,
        PayableAmount = 11550m,
    };

    public static EDocumentModel EArsivSatis() => new()
    {
        Kind = DocumentKind.EArsiv,
        ProfileId = UblProfileIds.EArsivFatura,
        TypeCode = UblTypeCodes.Satis,
        InvoiceNumber = "DIS2026000000001",
        Ettn = Ettn,
        IssueDate = new DateOnly(2026, 8, 20),
        IssueTime = new TimeOnly(14, 30, 0),
        Seller = CompanySeller(),
        Buyer = IndividualBuyer(),
        Lines = StandardLines(),
        Totals = StandardTotals(),
    };

    public static EDocumentModel EFaturaSatis() => EArsivSatis() with
    {
        Kind = DocumentKind.EFatura,
        ProfileId = UblProfileIds.TicariFatura,
        InvoiceNumber = "DIS2026000000002",
        Buyer = CorporateBuyer(),
    };

    public static EDocumentModel EFaturaTevkifat() => EArsivSatis() with
    {
        Kind = DocumentKind.EFatura,
        ProfileId = UblProfileIds.TicariFatura,
        TypeCode = UblTypeCodes.Tevkifat,
        InvoiceNumber = "DIS2026000000003",
        Buyer = GovernmentBuyer(),
        Withholding = new WithholdingInfo { Code = "616", Percent = 50m, Amount = 525m },
        Totals = StandardTotals() with
        {
            WithholdingVatTotal = 525m,
            PayableAmount = 11025m,
        },
    };

    public static EDocumentModel EArsivIstisna() => EArsivSatis() with
    {
        TypeCode = UblTypeCodes.Istisna,
        InvoiceNumber = "DIS2026000000004",
        Buyer = ForeignBuyer(),
        Lines = StandardLines(vatRate: 0m),
        Exemption = new ExemptionInfo
        {
            Code = "334",
            Reason = "KDV Kanunu 13/l — Yabancılara verilen sağlık hizmetlerinde istisna",
        },
        Totals = StandardTotals() with
        {
            VatTotal = 0m,
            TaxInclusiveAmount = 10500m,
            PayableAmount = 10500m,
        },
    };

    public static EDocumentModel EArsivIade() => EArsivSatis() with
    {
        TypeCode = UblTypeCodes.Iade,
        InvoiceNumber = "DIS2026000000005",
        SourceDocument = new SourceDocumentReference
        {
            InvoiceNumber = "DIS2026000000001",
            IssueDate = new DateOnly(2026, 7, 1),
        },
    };

    /// <summary>e-SMM: brüt 10000, KDV %10 = 1000, GV stopajı %20 = 2000, net 9000.</summary>
    public static EDocumentModel ESmm(bool vatRegisteredBuyer = true)
    {
        var buyer = vatRegisteredBuyer ? CorporateBuyer(gibUser: false) : IndividualBuyer();
        return new EDocumentModel
        {
            Kind = DocumentKind.ESmm,
            ProfileId = UblProfileIds.ESmm,
            TypeCode = UblTypeCodes.Satis,
            InvoiceNumber = "SMM2026000000001",
            Ettn = Ettn,
            IssueDate = new DateOnly(2026, 8, 20),
            IssueTime = new TimeOnly(9, 15, 0),
            Seller = SoleProprietorSeller(),
            Buyer = buyer,
            Lines =
            [
                new DocumentLine
                {
                    Name = "Kanal tedavisi",
                    Quantity = 1m,
                    UnitPrice = 10000m,
                    VatRate = 10m,
                    VatAmount = 1000m,
                    LineTotal = 10000m,
                },
            ],
            Totals = new DocumentTotals
            {
                LineExtensionTotal = 10000m,
                VatTotal = 1000m,
                GvStopajTotal = vatRegisteredBuyer ? 2000m : 0m,
                TaxExclusiveAmount = 10000m,
                TaxInclusiveAmount = 11000m,
                PayableAmount = vatRegisteredBuyer ? 9000m : 11000m,
            },
            GvStopaj = vatRegisteredBuyer ? new GvStopajInfo { Percent = 20m, Amount = 2000m } : null,
        };
    }
}
