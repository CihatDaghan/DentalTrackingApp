using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.EDocument.Ubl.Models;

namespace Dental.Infrastructure.Invoices;

/// <summary>Mapper'ın domain'den bağımsız kalması için gereken bağlam (satıcı ve kaynak belge bilgisi).</summary>
/// <param name="Tenant">Satıcı kiracı (unvan, VKN/TCKN, vergi dairesi, hukuki yapı).</param>
/// <param name="Clinic">Adres kaynağı.</param>
/// <param name="SellerFirstName">Şahıs hekimde cac:Person için ad.</param>
/// <param name="SellerLastName">Şahıs hekimde cac:Person için soyad.</param>
/// <param name="SellerEmail">Satıcı e-postası.</param>
/// <param name="WithholdingPercent">Tevkifat oranı (TaxConfig'ten; 5/10 → 50).</param>
/// <param name="GvStopajPercent">e-SMM GV stopaj oranı (varsayılan 20).</param>
/// <param name="SourceInvoiceNumber">IADE belgesinde kaynak fatura numarası.</param>
/// <param name="SourceIssueDate">IADE belgesinde kaynak fatura tarihi.</param>
public sealed record EDocumentMappingContext(
    Tenant Tenant,
    Clinic Clinic,
    string? SellerFirstName,
    string? SellerLastName,
    string? SellerEmail,
    decimal WithholdingPercent,
    decimal GvStopajPercent,
    string? SourceInvoiceNumber,
    DateOnly? SourceIssueDate);

/// <summary>
/// Invoice entity → sağlayıcıdan bağımsız <see cref="EDocumentModel"/> eşlemesi.
/// Kütüphane numara/ETTN üretmez; ikisi de burada zaten atanmış olarak gelir ((c)-1).
/// </summary>
public static class EDocumentMapper
{
    /// <summary>
    /// TCKN'si olmayan YABANCI GERÇEK KİŞİ (hasta) için kullanılan sabit kimlik numarası: 11 adet 1,
    /// schemeID="TCKN". GİB e-Fatura Paketi schematron'undaki <c>PartyIdentificationTCKNVKNCheck</c>
    /// kuralı uzunluğu bağlar: schemeID='TCKN' ⇒ 11 hane, schemeID='VKN' ⇒ 10 hane.
    /// (F aşaması araştırması: önceki 10 haneli "2222222222" varsayımı gerçek kişi için YANLIŞ
    /// kategoriydi — o değer yabancı TÜZEL kişi/aracı kurum içindir, bkz.
    /// <see cref="ForeignCorporateTaxId"/>.)
    /// </summary>
    public const string ForeignBuyerTaxId = "11111111111";

    /// <summary>TCKN/VKN'si olmayan yabancı TÜZEL kişi (aracı kurum, sigorta) — schemeID="VKN".</summary>
    public const string ForeignCorporateTaxId = "2222222222";

    public static EDocumentModel ToModel(Invoice invoice, EDocumentMappingContext context)
    {
        ArgumentNullException.ThrowIfNull(invoice);
        ArgumentNullException.ThrowIfNull(context);

        if (invoice.InvoiceNumber is null || invoice.Ettn is null)
            throw new InvalidOperationException("Belge numarası ve ETTN atanmadan UBL üretilemez.");

        return new EDocumentModel
        {
            Kind = MapKind(invoice.DocumentKind),
            ProfileId = invoice.ProfileId ?? "",
            TypeCode = invoice.TypeCode,
            InvoiceNumber = invoice.InvoiceNumber,
            Ettn = invoice.Ettn.Value,
            IssueDate = invoice.IssueDate,
            IssueTime = invoice.IssueTime,
            CurrencyCode = invoice.CurrencyCode,
            ExchangeRate = invoice.CurrencyCode == "TRY" ? null : invoice.ExchangeRate,
            Seller = MapSeller(context),
            Buyer = MapBuyer(invoice),
            Lines = [.. invoice.Lines.OrderBy(l => l.SeqNo).Select(MapLine)],
            Totals = MapTotals(invoice),
            Withholding = invoice.WithholdingCode is { } withholdingCode
                ? new WithholdingInfo
                {
                    Code = withholdingCode,
                    Percent = context.WithholdingPercent,
                    Amount = invoice.WithholdingTotal,
                }
                : null,
            Exemption = invoice.ExemptionCode is { } exemptionCode
                ? new ExemptionInfo
                {
                    Code = exemptionCode,
                    Reason = invoice.ExemptionReason ?? "",
                }
                : null,
            SourceDocument = context.SourceInvoiceNumber is { } sourceNumber && context.SourceIssueDate is { } sourceDate
                ? new SourceDocumentReference { InvoiceNumber = sourceNumber, IssueDate = sourceDate }
                : null,
            GvStopaj = invoice.GvStopajTotal > 0m
                ? new GvStopajInfo { Percent = context.GvStopajPercent, Amount = invoice.GvStopajTotal }
                : null,
        };
    }

    public static DocumentKind MapKind(InvoiceDocumentKind kind) => kind switch
    {
        InvoiceDocumentKind.EFatura => DocumentKind.EFatura,
        InvoiceDocumentKind.EArsiv => DocumentKind.EArsiv,
        InvoiceDocumentKind.ESmm => DocumentKind.ESmm,
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    public static SellerLegalType MapLegalType(TenantLegalType legalType) =>
        legalType == TenantLegalType.SoleProprietor ? SellerLegalType.SoleProprietor : SellerLegalType.Company;

    private static SellerInfo MapSeller(EDocumentMappingContext context)
    {
        var tenant = context.Tenant;
        if (string.IsNullOrWhiteSpace(tenant.TaxNumber))
            throw new InvalidOperationException("Kiracının VKN/TCKN bilgisi eksik; e-belge kesilemez.");

        var isSoleProprietor = tenant.LegalType == TenantLegalType.SoleProprietor;
        return new SellerInfo
        {
            Name = tenant.Name,
            // cac:Person yalnız şahıs hekimde (serbest meslek erbabı) yazılır.
            FirstName = isSoleProprietor ? context.SellerFirstName : null,
            LastName = isSoleProprietor ? context.SellerLastName : null,
            TaxId = tenant.TaxNumber,
            TaxOffice = string.IsNullOrWhiteSpace(tenant.TaxOffice) ? "-" : tenant.TaxOffice,
            Address = MapAddress(context.Clinic.Address, context.Clinic.District, context.Clinic.City),
            Email = context.SellerEmail ?? context.Clinic.Email,
        };
    }

    private static BuyerInfo MapBuyer(Invoice invoice)
    {
        var isCompany = invoice.CustomerType == InvoiceCustomerType.Company;
        var isForeign = !string.IsNullOrWhiteSpace(invoice.BuyerPassportNo);
        var (firstName, lastName) = isCompany ? (null, null) : SplitName(invoice.BuyerName);

        return new BuyerInfo
        {
            Kind = isCompany
                ? invoice.WithholdingCode is not null ? BuyerKind.Government : BuyerKind.Corporate
                : BuyerKind.IndividualPatient,
            CorporateName = isCompany ? invoice.BuyerName : null,
            FirstName = firstName,
            LastName = lastName,
            TaxId = invoice.BuyerTcknVkn,
            TaxOffice = invoice.BuyerTaxOffice,
            // (c)-3: GV stopajı yalnız vergi mükellefi alıcıya; kurum alıcı mükellef sayılır.
            IsVatRegistered = isCompany,
            IsGibEInvoiceUser = invoice.DocumentKind == InvoiceDocumentKind.EFatura,
            IsForeign = isForeign,
            PassportNumber = invoice.BuyerPassportNo,
            // Snapshot SKRS alfa-3 tutar; UBL alpha-2 ister (schematron $CountryCodeList).
            Nationality = NationalityCodes.ToAlpha2(invoice.BuyerNationality),
            LastEntryDate = invoice.BuyerLastEntryDate,
            Address = MapAddress(invoice.BuyerAddress, invoice.BuyerDistrict, invoice.BuyerCity),
            Email = invoice.BuyerEmail,
        };
    }

    private static AddressInfo MapAddress(string? street, string? district, string? city) => new()
    {
        StreetName = string.IsNullOrWhiteSpace(street) ? null : street,
        // UBL-TR'de il/ilçe zorunludur; eksikse belge reddedilmesin diye tire ile doldurulur
        // (önizleme bu durumu zaten uyarı olarak bildirir).
        CitySubdivisionName = string.IsNullOrWhiteSpace(district) ? "-" : district,
        CityName = string.IsNullOrWhiteSpace(city) ? "-" : city,
        CountryName = "Türkiye",
        CountryCode = "TR",
    };

    private static DocumentLine MapLine(InvoiceLine line) => new()
    {
        Name = line.ItemName,
        Quantity = line.Quantity,
        UnitCode = line.UnitCode,
        UnitPrice = line.UnitPrice,
        DiscountAmount = line.DiscountAmount,
        VatRate = line.VatRate,
        VatAmount = line.VatAmount,
        LineTotal = line.LineTotal,
        IsAesthetic = line.IsAesthetic,
    };

    private static DocumentTotals MapTotals(Invoice invoice) => new()
    {
        LineExtensionTotal = invoice.SubTotal,
        DiscountTotal = invoice.DiscountTotal,
        VatTotal = invoice.VatTotal,
        WithholdingVatTotal = invoice.WithholdingTotal,
        GvStopajTotal = invoice.GvStopajTotal,
        TaxExclusiveAmount = invoice.SubTotal,
        TaxInclusiveAmount = invoice.SubTotal + invoice.VatTotal,
        PayableAmount = invoice.PayableAmount,
    };

    /// <summary>
    /// Snapshot tek kolonda tutulduğu için ad/soyad son boşluktan ayrılır
    /// ("Mehmet Ali Kaya" → "Mehmet Ali" / "Kaya").
    /// </summary>
    internal static (string? First, string? Last) SplitName(string fullName)
    {
        var trimmed = fullName.Trim();
        var index = trimmed.LastIndexOf(' ');
        return index <= 0 ? (trimmed, "-") : (trimmed[..index], trimmed[(index + 1)..]);
    }
}
