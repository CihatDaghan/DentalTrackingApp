using System.Text;
using System.Xml.Linq;
using Dental.EDocument.Ubl.Builders;
using Dental.EDocument.Ubl.Models;

namespace Dental.Ubl.Tests;

public sealed class InvoiceUblBuilderTests
{
    private static readonly XNamespace Inv = UblNamespaces.Invoice;
    private static readonly XNamespace Cac = UblNamespaces.Cac;
    private static readonly XNamespace Cbc = UblNamespaces.Cbc;

    private readonly InvoiceUblBuilder _builder = new();

    private static XElement RootOf(XDocument document) => document.Root!;

    [Fact]
    public void Earsiv_root_element_is_invoice_with_correct_namespace()
    {
        var root = RootOf(_builder.Build(TestModels.EArsivSatis()));

        Assert.Equal(Inv + "Invoice", root.Name);
    }

    [Fact]
    public void Earsiv_header_fields_are_correct()
    {
        var model = TestModels.EArsivSatis();
        var root = RootOf(_builder.Build(model));

        Assert.Equal("2.1", root.Element(Cbc + "UBLVersionID")?.Value);
        Assert.Equal("TR1.2", root.Element(Cbc + "CustomizationID")?.Value);
        Assert.Equal("EARSIVFATURA", root.Element(Cbc + "ProfileID")?.Value);
        Assert.Equal("DIS2026000000001", root.Element(Cbc + "ID")?.Value);
        Assert.Equal(model.Ettn.ToString("D"), root.Element(Cbc + "UUID")?.Value);
        Assert.Equal("2026-08-20", root.Element(Cbc + "IssueDate")?.Value);
        Assert.Equal("14:30:00", root.Element(Cbc + "IssueTime")?.Value);
        Assert.Equal("SATIS", root.Element(Cbc + "InvoiceTypeCode")?.Value);
        Assert.Equal("TRY", root.Element(Cbc + "DocumentCurrencyCode")?.Value);
    }

    [Fact]
    public void Line_count_matches_lines()
    {
        var root = RootOf(_builder.Build(TestModels.EArsivSatis()));

        Assert.Equal(2, root.Elements(Cac + "InvoiceLine").Count());
        Assert.Equal("2", root.Element(Cbc + "LineCountNumeric")?.Value);
    }

    [Fact]
    public void Vat_uses_tax_type_code_0015()
    {
        var root = RootOf(_builder.Build(TestModels.EArsivSatis()));

        var taxTotal = root.Element(Cac + "TaxTotal");
        Assert.NotNull(taxTotal);
        var typeCode = taxTotal!
            .Element(Cac + "TaxSubtotal")?
            .Element(Cac + "TaxCategory")?
            .Element(Cac + "TaxScheme")?
            .Element(Cbc + "TaxTypeCode")?.Value;
        Assert.Equal("0015", typeCode);
    }

    [Fact]
    public void Amounts_use_invariant_two_decimals_and_currency_id()
    {
        var root = RootOf(_builder.Build(TestModels.EArsivSatis()));

        var taxAmount = root.Element(Cac + "TaxTotal")?.Element(Cbc + "TaxAmount");
        Assert.Equal("1050.00", taxAmount?.Value);
        Assert.Equal("TRY", taxAmount?.Attribute("currencyID")?.Value);

        var payable = root.Element(Cac + "LegalMonetaryTotal")?.Element(Cbc + "PayableAmount");
        Assert.Equal("11550.00", payable?.Value);
    }

    [Fact]
    public void Line_discount_is_written_as_allowance_charge()
    {
        var root = RootOf(_builder.Build(TestModels.EArsivSatis()));

        var firstLine = root.Elements(Cac + "InvoiceLine").First();
        var allowance = firstLine.Element(Cac + "AllowanceCharge");
        Assert.NotNull(allowance);
        Assert.Equal("false", allowance!.Element(Cbc + "ChargeIndicator")?.Value);
        Assert.Equal("500.00", allowance.Element(Cbc + "Amount")?.Value);
        Assert.Equal("10000.00", allowance.Element(Cbc + "BaseAmount")?.Value);

        var monetary = root.Element(Cac + "LegalMonetaryTotal");
        Assert.Equal("500.00", monetary?.Element(Cbc + "AllowanceTotalAmount")?.Value);
    }

    [Fact]
    public void Embedded_xslt_reference_contains_valid_base64_stylesheet()
    {
        var root = RootOf(_builder.Build(TestModels.EArsivSatis()));

        var reference = root.Elements(Cac + "AdditionalDocumentReference")
            .Single(r => r.Element(Cbc + "DocumentType")?.Value == "XSLT");
        var binary = reference.Element(Cac + "Attachment")?
            .Element(Cbc + "EmbeddedDocumentBinaryObject");

        Assert.NotNull(binary);
        Assert.Equal("Base64", binary!.Attribute("encodingCode")?.Value);
        var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(binary.Value));
        Assert.Contains("<xsl:stylesheet", decoded);
    }

    [Fact]
    public void Efatura_uses_ticari_profile()
    {
        var root = RootOf(_builder.Build(TestModels.EFaturaSatis()));

        Assert.Equal("TICARIFATURA", root.Element(Cbc + "ProfileID")?.Value);
        var buyerId = root.Element(Cac + "AccountingCustomerParty")?
            .Element(Cac + "Party")?
            .Element(Cac + "PartyIdentification")?
            .Element(Cbc + "ID");
        Assert.Equal("VKN", buyerId?.Attribute("schemeID")?.Value);
        Assert.Equal("9876543210", buyerId?.Value);
    }

    [Fact]
    public void Tevkifat_writes_withholding_block_with_code_616()
    {
        var root = RootOf(_builder.Build(TestModels.EFaturaTevkifat()));

        Assert.Equal("TEVKIFAT", root.Element(Cbc + "InvoiceTypeCode")?.Value);

        var withholding = root.Element(Cac + "WithholdingTaxTotal");
        Assert.NotNull(withholding);
        Assert.Equal("525.00", withholding!.Element(Cbc + "TaxAmount")?.Value);

        var subtotal = withholding.Element(Cac + "TaxSubtotal");
        Assert.Equal("50", subtotal?.Element(Cbc + "Percent")?.Value);

        // UBL-TR 1.2: WithholdingTaxTotal altında vergi kodu = tevkifat kodu (9015 değil).
        var scheme = subtotal?.Element(Cac + "TaxCategory")?.Element(Cac + "TaxScheme");
        Assert.Equal("616", scheme?.Element(Cbc + "TaxTypeCode")?.Value);
        Assert.Equal("KDV Tevkifatı", scheme?.Element(Cbc + "Name")?.Value);

        // Hesaplanan KDV yine 0015 ile tam gösterilir.
        var vatScheme = root.Element(Cac + "TaxTotal")?
            .Element(Cac + "TaxSubtotal")?
            .Element(Cac + "TaxCategory")?
            .Element(Cac + "TaxScheme");
        Assert.Equal("0015", vatScheme?.Element(Cbc + "TaxTypeCode")?.Value);

        // Ödenecek tutar tevkifat düşülmüş olmalı.
        Assert.Equal("11025.00",
            root.Element(Cac + "LegalMonetaryTotal")?.Element(Cbc + "PayableAmount")?.Value);
    }

    [Fact]
    public void Istisna_writes_exemption_code_and_reason()
    {
        var root = RootOf(_builder.Build(TestModels.EArsivIstisna()));

        Assert.Equal("ISTISNA", root.Element(Cbc + "InvoiceTypeCode")?.Value);

        var category = root.Element(Cac + "TaxTotal")?
            .Element(Cac + "TaxSubtotal")?
            .Element(Cac + "TaxCategory");
        Assert.Equal("334", category?.Element(Cbc + "TaxExemptionReasonCode")?.Value);
        Assert.Contains("13/l", category?.Element(Cbc + "TaxExemptionReason")?.Value);
        Assert.Equal("0.00", root.Element(Cac + "TaxTotal")?.Element(Cbc + "TaxAmount")?.Value);
    }

    [Fact]
    public void Foreign_patient_has_fixed_tckn_and_passport_identification()
    {
        var root = RootOf(_builder.Build(TestModels.EArsivIstisna()));

        var identifications = root.Element(Cac + "AccountingCustomerParty")!
            .Element(Cac + "Party")!
            .Elements(Cac + "PartyIdentification")
            .Select(p => p.Element(Cbc + "ID")!)
            .ToList();

        // DOĞRULANDI (schematron PartyIdentificationTCKNVKNCheck): schemeID='TCKN' ⇒ 11 hane.
        // Yabancı gerçek kişide 11 adet 1 kullanılır; 10 haneli "2222222222" tüzel kişi içindir.
        Assert.Contains(identifications,
            id => id.Attribute("schemeID")?.Value == "TCKN" && id.Value == "11111111111");
        Assert.Contains(identifications,
            id => id.Attribute("schemeID")?.Value == "PASAPORTNO" && id.Value == "P1234567");

        var person = root.Element(Cac + "AccountingCustomerParty")!
            .Element(Cac + "Party")!
            .Element(Cac + "Person");
        Assert.Equal("GB", person?.Element(Cbc + "NationalityID")?.Value);
        // GİB'in yolcu-beraberi kalıbı: pasaport ayrıca Person/IdentityDocumentReference altında.
        Assert.Equal("P1234567",
            person?.Element(Cac + "IdentityDocumentReference")?.Element(Cbc + "ID")?.Value);

        // Son giriş tarihi belge notunda gösterilir.
        Assert.Contains(root.Elements(Cbc + "Note"), n => n.Value.Contains("son giriş"));
    }

    [Fact]
    public void Iade_writes_billing_reference_to_source_document()
    {
        var root = RootOf(_builder.Build(TestModels.EArsivIade()));

        Assert.Equal("IADE", root.Element(Cbc + "InvoiceTypeCode")?.Value);

        var reference = root.Element(Cac + "BillingReference")?
            .Element(Cac + "InvoiceDocumentReference");
        Assert.NotNull(reference);
        Assert.Equal("DIS2026000000001", reference!.Element(Cbc + "ID")?.Value);
        Assert.Equal("2026-07-01", reference.Element(Cbc + "IssueDate")?.Value);
    }

    [Fact]
    public void Earsiv_without_issue_time_is_rejected()
    {
        var model = TestModels.EArsivSatis() with { IssueTime = null };

        Assert.Throws<InvalidOperationException>(() => _builder.Build(model));
    }

    [Fact]
    public void Esmm_model_is_rejected_by_invoice_builder()
    {
        // e-SMM Invoice olarak ÜRETİLEMEZ — CreditNote şarttır.
        Assert.False(_builder.CanBuild(DocumentKind.ESmm));
        Assert.Throws<InvalidOperationException>(() => _builder.Build(TestModels.ESmm()));
    }

    [Fact]
    public void Xml_string_output_declares_utf8()
    {
        var xml = _builder.BuildXmlString(TestModels.EArsivSatis());

        Assert.StartsWith("<?xml version=\"1.0\" encoding=\"utf-8\"?>", xml,
            StringComparison.OrdinalIgnoreCase);
    }
}
