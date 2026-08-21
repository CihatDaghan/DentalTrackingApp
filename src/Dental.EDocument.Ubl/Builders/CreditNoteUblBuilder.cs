using System.Globalization;
using System.Xml.Linq;
using Dental.EDocument.Ubl.Models;

namespace Dental.EDocument.Ubl.Builders;

/// <summary>
/// e-SMM için UBL 2.1 CreditNote üretir (Invoice DEĞİL — sık yapılan hata).
/// CreditNoteTypeCode yazılmaz; e-SMM kılavuzunda tip kodu alanı kullanılmıyor (açık madde).
/// </summary>
public sealed class CreditNoteUblBuilder : IUblDocumentBuilder
{
    private static readonly XNamespace Cn = UblNamespaces.CreditNote;
    private static readonly XNamespace Cac = UblNamespaces.Cac;
    private static readonly XNamespace Cbc = UblNamespaces.Cbc;

    public bool CanBuild(DocumentKind kind) => kind == DocumentKind.ESmm;

    public XDocument Build(EDocumentModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        Validate(model);

        var root = new XElement(Cn + "CreditNote",
            new XAttribute(XNamespace.Xmlns + "cac", UblNamespaces.Cac),
            new XAttribute(XNamespace.Xmlns + "cbc", UblNamespaces.Cbc),
            new XAttribute(XNamespace.Xmlns + "ext", UblNamespaces.Ext),
            new XElement(Cbc + "UBLVersionID", UblVersions.UblVersionId),
            // CreditNote ailesi TR1.2.1 kullanır (Invoice tarafı TR1.2) — GİB'in yayımlanmış
            // e-Müstahsil / e-Gider Pusulası / e-Döviz kılavuzlarının üçü de böyle.
            new XElement(Cbc + "CustomizationID", UblVersions.CreditNoteCustomizationId),
            new XElement(Cbc + "ProfileID", model.ProfileId),
            new XElement(Cbc + "ID", model.InvoiceNumber),
            new XElement(Cbc + "CopyIndicator", "false"),
            new XElement(Cbc + "UUID", model.Ettn.ToString("D")),
            new XElement(Cbc + "IssueDate", UblFormat.Date(model.IssueDate)));

        if (model.IssueTime is { } issueTime)
        {
            root.Add(new XElement(Cbc + "IssueTime", UblFormat.Time(issueTime)));
        }

        // AÇIK MADDE: e-SMM'de tip kodunun hangi değeri alacağı teyit edilemedi; model doldurulmadıkça
        // yazılmaz. UBL şemasında CreditNoteTypeCode IssueTime'dan hemen sonra gelir.
        if (!string.IsNullOrWhiteSpace(model.CreditNoteTypeCode))
        {
            root.Add(new XElement(Cbc + "CreditNoteTypeCode", model.CreditNoteTypeCode));
        }

        foreach (var note in model.Notes)
        {
            root.Add(new XElement(Cbc + "Note", note));
        }

        root.Add(new XElement(Cbc + "DocumentCurrencyCode", model.CurrencyCode));
        root.Add(new XElement(Cbc + "LineCountNumeric",
            model.Lines.Count.ToString(CultureInfo.InvariantCulture)));

        if (model.SourceDocument is { } source)
        {
            root.Add(UblFragments.BillingReference(source));
        }

        root.Add(UblFragments.XsltDocumentReference(model));
        root.Add(UblFragments.Signature(model.Seller));
        root.Add(UblFragments.SupplierParty(model.Seller));
        root.Add(UblFragments.CustomerParty(model.Buyer));

        if (UblFragments.PricingExchangeRate(model) is { } exchangeRate)
        {
            root.Add(exchangeRate);
        }

        root.Add(UblFragments.DocumentTaxTotal(model));

        // (c)-3: GV stopajı yalnız vergi mükellefi alıcıda — model bu kuralı geçmişse GvStopaj doludur.
        // CreditNote şemasında WithholdingTaxTotal olmadığından ayrı TaxTotal (0003) yazılır.
        if (model.GvStopaj is { } stopaj)
        {
            root.Add(UblFragments.GvStopajTaxTotal(stopaj, model.Totals.LineExtensionTotal, model.CurrencyCode));
        }

        root.Add(UblFragments.LegalMonetaryTotal(model));

        var lineId = 0;
        foreach (var line in model.Lines)
        {
            root.Add(CreditNoteLine(line, ++lineId, model));
        }

        return new XDocument(new XDeclaration("1.0", "UTF-8", null), root);
    }

    private static XElement CreditNoteLine(DocumentLine line, int id, EDocumentModel model)
    {
        var currency = model.CurrencyCode;
        var element = new XElement(Cac + "CreditNoteLine",
            new XElement(Cbc + "ID", id.ToString(CultureInfo.InvariantCulture)),
            new XElement(Cbc + "CreditedQuantity",
                new XAttribute("unitCode", line.UnitCode), UblFormat.Quantity(line.Quantity)),
            UblFragments.Amount(Cbc + "LineExtensionAmount", line.LineTotal, currency));

        if (UblFragments.LineAllowance(line, currency) is { } allowance)
        {
            element.Add(allowance);
        }

        element.Add(UblFragments.LineTaxTotal(line, model.Exemption, currency));
        element.Add(UblFragments.Item(line));
        element.Add(UblFragments.Price(line, currency));
        return element;
    }

    private static void Validate(EDocumentModel model)
    {
        if (model.Kind != DocumentKind.ESmm)
        {
            throw new InvalidOperationException(
                "CreditNoteUblBuilder yalnız e-SMM üretir; e-Fatura/e-Arşiv için InvoiceUblBuilder kullanın.");
        }

        // (c)-3: bireysel hastaya stopaj kesilmez.
        if (model.GvStopaj is not null && !model.Buyer.IsVatRegistered)
        {
            throw new InvalidOperationException(
                "GV stopajı yalnız vergi mükellefi alıcıya uygulanır; bireysel hastaya stopaj kesilemez.");
        }

        if (model.Lines.Count == 0)
        {
            throw new InvalidOperationException("Belgede en az bir satır olmalıdır.");
        }
    }
}
