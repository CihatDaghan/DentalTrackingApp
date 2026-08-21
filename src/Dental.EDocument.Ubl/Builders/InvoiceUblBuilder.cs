using System.Globalization;
using System.Xml.Linq;
using Dental.EDocument.Ubl.Models;

namespace Dental.EDocument.Ubl.Builders;

/// <summary>
/// e-Fatura ve e-Arşiv için UBL 2.1 Invoice üretir.
/// e-SMM bu builder'dan ÜRETİLEMEZ — o UBL CreditNote'tur (CreditNoteUblBuilder).
/// </summary>
public sealed class InvoiceUblBuilder : IUblDocumentBuilder
{
    private static readonly XNamespace Inv = UblNamespaces.Invoice;
    private static readonly XNamespace Cac = UblNamespaces.Cac;
    private static readonly XNamespace Cbc = UblNamespaces.Cbc;

    public bool CanBuild(DocumentKind kind) => kind is DocumentKind.EFatura or DocumentKind.EArsiv;

    public XDocument Build(EDocumentModel model)
    {
        ArgumentNullException.ThrowIfNull(model);
        Validate(model);

        var root = new XElement(Inv + "Invoice",
            new XAttribute(XNamespace.Xmlns + "cac", UblNamespaces.Cac),
            new XAttribute(XNamespace.Xmlns + "cbc", UblNamespaces.Cbc),
            new XAttribute(XNamespace.Xmlns + "ext", UblNamespaces.Ext),
            new XElement(Cbc + "UBLVersionID", UblVersions.UblVersionId),
            new XElement(Cbc + "CustomizationID", UblVersions.CustomizationId),
            new XElement(Cbc + "ProfileID", model.ProfileId),
            new XElement(Cbc + "ID", model.InvoiceNumber),
            new XElement(Cbc + "CopyIndicator", "false"),
            new XElement(Cbc + "UUID", model.Ettn.ToString("D")),
            new XElement(Cbc + "IssueDate", UblFormat.Date(model.IssueDate)));

        if (model.IssueTime is { } issueTime)
        {
            root.Add(new XElement(Cbc + "IssueTime", UblFormat.Time(issueTime)));
        }

        root.Add(new XElement(Cbc + "InvoiceTypeCode", model.TypeCode));

        foreach (var note in BuildNotes(model))
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

        if (model.Withholding is { } withholding)
        {
            root.Add(UblFragments.WithholdingTaxTotal(withholding, model.Totals.VatTotal, model.CurrencyCode));
        }

        root.Add(UblFragments.LegalMonetaryTotal(model));

        var lineId = 0;
        foreach (var line in model.Lines)
        {
            root.Add(InvoiceLine(line, ++lineId, model));
        }

        return new XDocument(new XDeclaration("1.0", "UTF-8", null), root);
    }

    private static XElement InvoiceLine(DocumentLine line, int id, EDocumentModel model)
    {
        var currency = model.CurrencyCode;
        var element = new XElement(Cac + "InvoiceLine",
            new XElement(Cbc + "ID", id.ToString(CultureInfo.InvariantCulture)),
            new XElement(Cbc + "InvoicedQuantity",
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

    private static IEnumerable<string> BuildNotes(EDocumentModel model)
    {
        foreach (var note in model.Notes)
        {
            yield return note;
        }

        // 334 istisnasında son giriş tarihi faturada gösterilmek zorundadır (alan yerleşimi açık madde).
        if (model.Buyer.IsForeign && model.Buyer.LastEntryDate is { } lastEntry)
        {
            yield return $"Türkiye'ye son giriş tarihi: {UblFormat.Date(lastEntry)}";
        }
    }

    private static void Validate(EDocumentModel model)
    {
        if (model.Kind is not (DocumentKind.EFatura or DocumentKind.EArsiv))
        {
            throw new InvalidOperationException(
                "InvoiceUblBuilder yalnız e-Fatura/e-Arşiv üretir; e-SMM için CreditNoteUblBuilder kullanın.");
        }

        if (model.Kind == DocumentKind.EArsiv && model.IssueTime is null)
        {
            throw new InvalidOperationException("e-Arşiv belgesinde IssueTime zorunludur.");
        }

        if (model.TypeCode == UblTypeCodes.Tevkifat && model.Withholding is null)
        {
            throw new InvalidOperationException("TEVKIFAT belgesinde Withholding bilgisi zorunludur.");
        }

        if (model.TypeCode == UblTypeCodes.Istisna && model.Exemption is null)
        {
            throw new InvalidOperationException("ISTISNA belgesinde Exemption bilgisi zorunludur.");
        }

        if (model.TypeCode == UblTypeCodes.Iade && model.SourceDocument is null)
        {
            throw new InvalidOperationException("IADE belgesinde SourceDocument (kaynak belge) zorunludur.");
        }

        if (model.Lines.Count == 0)
        {
            throw new InvalidOperationException("Belgede en az bir satır olmalıdır.");
        }
    }
}
