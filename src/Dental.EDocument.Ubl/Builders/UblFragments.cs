using System.Xml.Linq;
using Dental.EDocument.Ubl.Models;

namespace Dental.EDocument.Ubl.Builders;

/// <summary>Invoice ve CreditNote üreticilerinin paylaştığı UBL parçaları.</summary>
internal static class UblFragments
{
    private static readonly XNamespace Cac = UblNamespaces.Cac;
    private static readonly XNamespace Cbc = UblNamespaces.Cbc;

    public static XElement Amount(XName name, decimal value, string currency) =>
        new(name, new XAttribute("currencyID", currency), UblFormat.Amount(value));

    /// <summary>Görüntüleme şablonu: base64 gömülü XSLT (GİB zorunluluğu).</summary>
    public static XElement XsltDocumentReference(EDocumentModel model) =>
        new(Cac + "AdditionalDocumentReference",
            new XElement(Cbc + "ID", model.Ettn.ToString("D")),
            new XElement(Cbc + "IssueDate", UblFormat.Date(model.IssueDate)),
            new XElement(Cbc + "DocumentType", "XSLT"),
            new XElement(Cac + "Attachment",
                new XElement(Cbc + "EmbeddedDocumentBinaryObject",
                    new XAttribute("mimeCode", "application/xml"),
                    new XAttribute("encodingCode", "Base64"),
                    new XAttribute("characterSetCode", "UTF-8"),
                    new XAttribute("filename", $"{model.InvoiceNumber}.xslt"),
                    EmbeddedXslt.GetBase64())));

    /// <summary>
    /// Mali mühür yer tutucusu — imzayı entegratör atar; blok imzayı değil imza sahibinin
    /// kimlik/adres bilgisini taşır ve GİB kılavuzlarında ZORUNLUdur (tam 1 adet).
    /// DOĞRULANDI: schematron SignatureCheck kuralı cbc:ID/@schemeID='VKN_TCKN' olmasını zorunlu kılar,
    /// SignatoryPartyPartyIdentificationCheck ise SignatoryParty altında schemeID='VKN' veya 'TCKN'
    /// olan en az bir PartyIdentification bekler.
    /// BİLİNEN SINIR: ExternalReference/URI değeri ("#Signature") ext:UBLExtensions içindeki
    /// ds:Signature/@Id ile eşleşmelidir; mührü entegratör attığı için o Id'yi entegratör üretir.
    /// İlgili schematron assert'leri yorum satırında olduğundan belge reddedilmez.
    /// </summary>
    public static XElement Signature(SellerInfo seller)
    {
        var schemeId = seller.IsPerson ? "TCKN" : "VKN";
        return new XElement(Cac + "Signature",
            new XElement(Cbc + "ID", new XAttribute("schemeID", "VKN_TCKN"), seller.TaxId),
            new XElement(Cac + "SignatoryParty",
                new XElement(Cac + "PartyIdentification",
                    new XElement(Cbc + "ID", new XAttribute("schemeID", schemeId), seller.TaxId)),
                PostalAddress(seller.Address)),
            new XElement(Cac + "DigitalSignatureAttachment",
                new XElement(Cac + "ExternalReference",
                    new XElement(Cbc + "URI", "#Signature"))));
    }

    /// <summary>İade belgesinde kaynak belge referansı.</summary>
    public static XElement BillingReference(SourceDocumentReference source) =>
        new(Cac + "BillingReference",
            new XElement(Cac + "InvoiceDocumentReference",
                new XElement(Cbc + "ID", source.InvoiceNumber),
                new XElement(Cbc + "IssueDate", UblFormat.Date(source.IssueDate))));

    public static XElement SupplierParty(SellerInfo seller)
    {
        var schemeId = seller.IsPerson ? "TCKN" : "VKN";
        var party = new XElement(Cac + "Party",
            new XElement(Cac + "PartyIdentification",
                new XElement(Cbc + "ID", new XAttribute("schemeID", schemeId), seller.TaxId)),
            new XElement(Cac + "PartyName", new XElement(Cbc + "Name", seller.Name)),
            PostalAddress(seller.Address),
            new XElement(Cac + "PartyTaxScheme",
                new XElement(Cac + "TaxScheme", new XElement(Cbc + "Name", seller.TaxOffice))));

        if (!string.IsNullOrWhiteSpace(seller.Email))
        {
            party.Add(new XElement(Cac + "Contact", new XElement(Cbc + "ElectronicMail", seller.Email)));
        }

        // Şahıs hekimde Person bloğu beklenir.
        if (!string.IsNullOrWhiteSpace(seller.FirstName) && !string.IsNullOrWhiteSpace(seller.LastName))
        {
            party.Add(new XElement(Cac + "Person",
                new XElement(Cbc + "FirstName", seller.FirstName),
                new XElement(Cbc + "FamilyName", seller.LastName)));
        }

        return new XElement(Cac + "AccountingSupplierParty", party);
    }

    public static XElement CustomerParty(BuyerInfo buyer)
    {
        var party = new XElement(Cac + "Party");

        if (!string.IsNullOrWhiteSpace(buyer.TaxId))
        {
            var schemeId = buyer.TaxId.Length == 11 ? "TCKN" : "VKN";
            party.Add(new XElement(Cac + "PartyIdentification",
                new XElement(Cbc + "ID", new XAttribute("schemeID", schemeId), buyer.TaxId)));
        }

        // Yabancı hastada pasaport: schemeID="PASAPORTNO". DOĞRULANDI — UBL-TR Kod Listeleri
        // "Alıcı/Satıcı Numarası (PartyIdentification)" ve GİB e-Fatura Paketi schematron'undaki
        // PartyIdentificationIDType listesi bu değeri içerir ("PASSPORT" geçersizdir).
        if (buyer.IsForeign && !string.IsNullOrWhiteSpace(buyer.PassportNumber))
        {
            party.Add(new XElement(Cac + "PartyIdentification",
                new XElement(Cbc + "ID", new XAttribute("schemeID", "PASAPORTNO"), buyer.PassportNumber)));
        }

        if (!string.IsNullOrWhiteSpace(buyer.CorporateName))
        {
            party.Add(new XElement(Cac + "PartyName", new XElement(Cbc + "Name", buyer.CorporateName)));
        }

        party.Add(buyer.Address is { } address
            ? PostalAddress(address)
            : new XElement(Cac + "PostalAddress",
                new XElement(Cac + "Country", new XElement(Cbc + "Name", "Türkiye"))));

        if (!string.IsNullOrWhiteSpace(buyer.TaxOffice))
        {
            party.Add(new XElement(Cac + "PartyTaxScheme",
                new XElement(Cac + "TaxScheme", new XElement(Cbc + "Name", buyer.TaxOffice))));
        }

        if (!string.IsNullOrWhiteSpace(buyer.Email))
        {
            party.Add(new XElement(Cac + "Contact", new XElement(Cbc + "ElectronicMail", buyer.Email)));
        }

        // Bireysel hastada Person zorunlu. Yabancıda GİB'in yolcu-beraberi (TAXFREE) kalıbı esas alınır:
        // uyruk cbc:NationalityID (ISO 3166-1 alpha-2), pasaport ayrıca
        // cac:IdentityDocumentReference/cbc:ID — resmî schematron bu ikisini birlikte doğrular.
        if (!string.IsNullOrWhiteSpace(buyer.FirstName) && !string.IsNullOrWhiteSpace(buyer.LastName))
        {
            var person = new XElement(Cac + "Person",
                new XElement(Cbc + "FirstName", buyer.FirstName),
                new XElement(Cbc + "FamilyName", buyer.LastName));

            if (buyer.IsForeign && !string.IsNullOrWhiteSpace(buyer.Nationality))
            {
                person.Add(new XElement(Cbc + "NationalityID", buyer.Nationality));
            }

            if (buyer.IsForeign && !string.IsNullOrWhiteSpace(buyer.PassportNumber))
            {
                // Şema sırası: NationalityID ... sonra cac:IdentityDocumentReference.
                person.Add(new XElement(Cac + "IdentityDocumentReference",
                    new XElement(Cbc + "ID", buyer.PassportNumber)));
            }

            party.Add(person);
        }

        return new XElement(Cac + "AccountingCustomerParty", party);
    }

    public static XElement PostalAddress(AddressInfo address)
    {
        var element = new XElement(Cac + "PostalAddress");

        if (!string.IsNullOrWhiteSpace(address.StreetName))
        {
            element.Add(new XElement(Cbc + "StreetName", address.StreetName));
        }

        element.Add(new XElement(Cbc + "CitySubdivisionName", address.CitySubdivisionName));
        element.Add(new XElement(Cbc + "CityName", address.CityName));

        if (!string.IsNullOrWhiteSpace(address.PostalZone))
        {
            element.Add(new XElement(Cbc + "PostalZone", address.PostalZone));
        }

        var country = new XElement(Cac + "Country");
        if (!string.IsNullOrWhiteSpace(address.CountryCode))
        {
            country.Add(new XElement(Cbc + "IdentificationCode", address.CountryCode));
        }

        country.Add(new XElement(Cbc + "Name", address.CountryName));
        element.Add(country);

        return element;
    }

    /// <summary>Dövizli belgede TRY kuru.</summary>
    public static XElement? PricingExchangeRate(EDocumentModel model)
    {
        if (model.ExchangeRate is not { } rate || model.CurrencyCode == "TRY")
        {
            return null;
        }

        return new XElement(Cac + "PricingExchangeRate",
            new XElement(Cbc + "SourceCurrencyCode", model.CurrencyCode),
            new XElement(Cbc + "TargetCurrencyCode", "TRY"),
            new XElement(Cbc + "CalculationRate", UblFormat.Rate(rate)),
            new XElement(Cbc + "Date", UblFormat.Date(model.IssueDate)));
    }

    /// <summary>Belge düzeyi KDV bloğu (0015) — oran bazında alt toplamlar.</summary>
    public static XElement DocumentTaxTotal(EDocumentModel model)
    {
        var currency = model.CurrencyCode;
        var taxTotal = new XElement(Cac + "TaxTotal",
            Amount(Cbc + "TaxAmount", model.Totals.VatTotal, currency));

        foreach (var group in model.Lines.GroupBy(l => l.VatRate).OrderBy(g => g.Key))
        {
            taxTotal.Add(VatSubtotal(
                taxable: group.Sum(l => l.LineTotal),
                tax: group.Sum(l => l.VatAmount),
                percent: group.Key,
                exemption: group.Key == 0m ? model.Exemption : null,
                currency));
        }

        return taxTotal;
    }

    /// <summary>Satır düzeyi KDV bloğu.</summary>
    public static XElement LineTaxTotal(DocumentLine line, ExemptionInfo? exemption, string currency) =>
        new(Cac + "TaxTotal",
            Amount(Cbc + "TaxAmount", line.VatAmount, currency),
            VatSubtotal(line.LineTotal, line.VatAmount, line.VatRate,
                line.VatRate == 0m ? exemption : null, currency));

    private static XElement VatSubtotal(
        decimal taxable, decimal tax, decimal percent, ExemptionInfo? exemption, string currency)
    {
        var category = new XElement(Cac + "TaxCategory");
        if (exemption is not null)
        {
            // İstisnada gerekçe kodu + metni zorunlu.
            category.Add(new XElement(Cbc + "TaxExemptionReasonCode", exemption.Code));
            category.Add(new XElement(Cbc + "TaxExemptionReason", exemption.Reason));
        }

        category.Add(new XElement(Cac + "TaxScheme",
            new XElement(Cbc + "Name", "KDV"),
            new XElement(Cbc + "TaxTypeCode", UblTaxTypeCodes.Kdv)));

        return new XElement(Cac + "TaxSubtotal",
            Amount(Cbc + "TaxableAmount", taxable, currency),
            Amount(Cbc + "TaxAmount", tax, currency),
            new XElement(Cbc + "Percent", UblFormat.Percent(percent)),
            category);
    }

    /// <summary>
    /// KDV tevkifatı. UBL-TR Kod Listeleri V1.18'e göre WithholdingTaxTotal altındaki
    /// vergi kodu TEVKİFAT KODLARI listesinden gelir (ör. 616) — eski 9015 yerine.
    /// </summary>
    public static XElement WithholdingTaxTotal(WithholdingInfo withholding, decimal vatTotal, string currency) =>
        new(Cac + "WithholdingTaxTotal",
            Amount(Cbc + "TaxAmount", withholding.Amount, currency),
            new XElement(Cac + "TaxSubtotal",
                Amount(Cbc + "TaxableAmount", vatTotal, currency),
                Amount(Cbc + "TaxAmount", withholding.Amount, currency),
                new XElement(Cbc + "Percent", UblFormat.Percent(withholding.Percent)),
                new XElement(Cac + "TaxCategory",
                    new XElement(Cac + "TaxScheme",
                        new XElement(Cbc + "Name", "KDV Tevkifatı"),
                        new XElement(Cbc + "TaxTypeCode", withholding.Code)))));

    /// <summary>
    /// e-SMM GV stopajı (0003), KDV'den ayrı ikinci bir TaxTotal bloğu olarak.
    ///
    /// DOĞRULANDI (F aşaması araştırması): UBL 2.1 CreditNote şemasında <c>cac:WithholdingTaxTotal</c>
    /// YOKTUR (yalnız Invoice'ta ve InvoiceLine'da vardır), bu yüzden stopaj ikinci bir TaxTotal
    /// olarak yazılmak zorundadır. GİB'in kendi çözümü de aynıdır: e-Müstahsil Makbuzu Teknik
    /// Kılavuzu V1.1 §2.3.17, müstahsil stopajını tam bu kalıpla (ayrı TaxTotal + TaxTypeCode 0003)
    /// gösterir. Kod: UBL-TR Kod Listeleri "0003 — Gelir Vergisi Stopajı".
    /// </summary>
    public static XElement GvStopajTaxTotal(GvStopajInfo stopaj, decimal grossAmount, string currency) =>
        new(Cac + "TaxTotal",
            Amount(Cbc + "TaxAmount", stopaj.Amount, currency),
            new XElement(Cac + "TaxSubtotal",
                Amount(Cbc + "TaxableAmount", grossAmount, currency),
                Amount(Cbc + "TaxAmount", stopaj.Amount, currency),
                new XElement(Cbc + "Percent", UblFormat.Percent(stopaj.Percent)),
                new XElement(Cac + "TaxCategory",
                    new XElement(Cac + "TaxScheme",
                        // GİB e-Müstahsil kılavuzundaki resmî ad; schematron KDV dışı vergilerde
                        // Name alanının dolu olmasını bekler.
                        new XElement(Cbc + "Name", "GELİR VERGİSİ S. (MUHTASAR)"),
                        new XElement(Cbc + "TaxTypeCode", UblTaxTypeCodes.GvStopaj)))));

    public static XElement LegalMonetaryTotal(EDocumentModel model)
    {
        var currency = model.CurrencyCode;
        var totals = model.Totals;
        var element = new XElement(Cac + "LegalMonetaryTotal",
            Amount(Cbc + "LineExtensionAmount", totals.LineExtensionTotal, currency),
            Amount(Cbc + "TaxExclusiveAmount", totals.TaxExclusiveAmount, currency),
            Amount(Cbc + "TaxInclusiveAmount", totals.TaxInclusiveAmount, currency));

        if (totals.DiscountTotal > 0m)
        {
            element.Add(Amount(Cbc + "AllowanceTotalAmount", totals.DiscountTotal, currency));
        }

        element.Add(Amount(Cbc + "PayableAmount", totals.PayableAmount, currency));
        return element;
    }

    /// <summary>Satır iskontosu (ChargeIndicator=false).</summary>
    public static XElement? LineAllowance(DocumentLine line, string currency)
    {
        if (line.DiscountAmount <= 0m)
        {
            return null;
        }

        return new XElement(Cac + "AllowanceCharge",
            new XElement(Cbc + "ChargeIndicator", "false"),
            Amount(Cbc + "Amount", line.DiscountAmount, currency),
            Amount(Cbc + "BaseAmount", line.Quantity * line.UnitPrice, currency));
    }

    public static XElement Item(DocumentLine line) =>
        new(Cac + "Item", new XElement(Cbc + "Name", line.Name));

    public static XElement Price(DocumentLine line, string currency) =>
        new(Cac + "Price", Amount(Cbc + "PriceAmount", line.UnitPrice, currency));
}
