using System.Xml.Linq;
using Dental.EDocument.Ubl.Builders;
using Dental.EDocument.Ubl.Models;

namespace Dental.Ubl.Tests;

public sealed class CreditNoteUblBuilderTests
{
    private static readonly XNamespace Cn = UblNamespaces.CreditNote;
    private static readonly XNamespace Cac = UblNamespaces.Cac;
    private static readonly XNamespace Cbc = UblNamespaces.Cbc;

    private readonly CreditNoteUblBuilder _builder = new();

    private static XElement RootOf(XDocument document) => document.Root!;

    [Fact]
    public void Esmm_root_element_is_credit_note_not_invoice()
    {
        var root = RootOf(_builder.Build(TestModels.ESmm()));

        Assert.Equal(Cn + "CreditNote", root.Name);
        Assert.Equal("urn:oasis:names:specification:ubl:schema:xsd:CreditNote-2",
            root.Name.NamespaceName);
    }

    [Fact]
    public void Esmm_header_fields_are_correct()
    {
        var model = TestModels.ESmm();
        var root = RootOf(_builder.Build(model));

        Assert.Equal("2.1", root.Element(Cbc + "UBLVersionID")?.Value);
        // CreditNote ailesi TR1.2.1 kullanır (Invoice tarafı TR1.2) — GİB e-Müstahsil /
        // e-Gider Pusulası / e-Döviz kılavuzlarının üçü de böyle.
        Assert.Equal("TR1.2.1", root.Element(Cbc + "CustomizationID")?.Value);
        // (c)-2: ProfileID boş bırakılmaz — EARSIVBELGE açık madde.
        Assert.Equal("EARSIVBELGE", root.Element(Cbc + "ProfileID")?.Value);
        Assert.Equal("SMM2026000000001", root.Element(Cbc + "ID")?.Value);
        Assert.Equal(model.Ettn.ToString("D"), root.Element(Cbc + "UUID")?.Value);
    }

    [Fact]
    public void Esmm_lines_are_credit_note_lines()
    {
        var root = RootOf(_builder.Build(TestModels.ESmm()));

        Assert.Empty(root.Elements(Cac + "InvoiceLine"));
        var line = Assert.Single(root.Elements(Cac + "CreditNoteLine"));
        var quantity = line.Element(Cbc + "CreditedQuantity");
        Assert.Equal("1", quantity?.Value);
        Assert.Equal("C62", quantity?.Attribute("unitCode")?.Value);
    }

    [Fact]
    public void Esmm_has_no_invoice_type_code()
    {
        // CreditNote'ta InvoiceTypeCode ŞEMADA YOKTUR; hiçbir koşulda yazılmaz.
        // CreditNoteTypeCode ise açık maddedir: doğru değer teyit edilemediği için model
        // doldurulmadıkça yazılmaz (aşağıdaki test dolduğunda yazıldığını doğrular).
        var root = RootOf(_builder.Build(TestModels.ESmm()));

        Assert.Null(root.Element(Cbc + "InvoiceTypeCode"));
        Assert.Null(root.Element(Cbc + "CreditNoteTypeCode"));
    }

    [Fact]
    public void Esmm_writes_credit_note_type_code_when_model_supplies_it()
    {
        // AÇIK MADDE: e-SMM tip kodunun değeri GİB tarafından yayımlanmadı. Entegratör teyidi
        // gelince tek alan doldurularak açılır; şema sırası IssueTime'dan hemen sonradır.
        var model = TestModels.ESmm() with { CreditNoteTypeCode = "SERBESTMESLEKMAKBUZU" };
        var root = RootOf(_builder.Build(model));

        Assert.Equal("SERBESTMESLEKMAKBUZU", root.Element(Cbc + "CreditNoteTypeCode")?.Value);
        var names = root.Elements().Select(e => e.Name.LocalName).ToList();
        Assert.Equal(names.IndexOf("IssueTime") + 1, names.IndexOf("CreditNoteTypeCode"));
    }

    [Fact]
    public void Esmm_vat_uses_0015()
    {
        var root = RootOf(_builder.Build(TestModels.ESmm()));

        var typeCode = root.Element(Cac + "TaxTotal")?
            .Element(Cac + "TaxSubtotal")?
            .Element(Cac + "TaxCategory")?
            .Element(Cac + "TaxScheme")?
            .Element(Cbc + "TaxTypeCode")?.Value;
        Assert.Equal("0015", typeCode);
    }

    private static string? TaxTypeCodeOf(XElement taxTotal) => taxTotal
        .Element(Cac + "TaxSubtotal")?
        .Element(Cac + "TaxCategory")?
        .Element(Cac + "TaxScheme")?
        .Element(Cbc + "TaxTypeCode")?.Value;

    [Fact]
    public void Esmm_with_vat_registered_buyer_writes_gv_stopaj_block()
    {
        var root = RootOf(_builder.Build(TestModels.ESmm(vatRegisteredBuyer: true)));

        // CreditNote şemasında WithholdingTaxTotal yok; stopaj ayrı TaxTotal (0003) bloğudur.
        var stopaj = root.Elements(Cac + "TaxTotal").Single(t => TaxTypeCodeOf(t) == "0003");
        Assert.Equal("2000.00", stopaj.Element(Cbc + "TaxAmount")?.Value);

        var subtotal = stopaj.Element(Cac + "TaxSubtotal");
        Assert.Equal("20", subtotal?.Element(Cbc + "Percent")?.Value);
        Assert.Equal("10000.00", subtotal?.Element(Cbc + "TaxableAmount")?.Value);

        // Net: 10000 + 1000 KDV − 2000 stopaj.
        Assert.Equal("9000.00",
            root.Element(Cac + "LegalMonetaryTotal")?.Element(Cbc + "PayableAmount")?.Value);
    }

    [Fact]
    public void Esmm_for_individual_patient_has_no_stopaj_block()
    {
        // (c)-3: bireysel hastaya stopaj YOK.
        var root = RootOf(_builder.Build(TestModels.ESmm(vatRegisteredBuyer: false)));

        Assert.DoesNotContain(root.Elements(Cac + "TaxTotal"), t => TaxTypeCodeOf(t) == "0003");
        Assert.Equal("11000.00",
            root.Element(Cac + "LegalMonetaryTotal")?.Element(Cbc + "PayableAmount")?.Value);
    }

    [Fact]
    public void Stopaj_on_individual_patient_model_is_rejected()
    {
        // (c)-3 koruması: model yanlış kurulursa builder reddeder.
        var model = TestModels.ESmm(vatRegisteredBuyer: false) with
        {
            GvStopaj = new Dental.EDocument.Ubl.Models.GvStopajInfo { Amount = 2000m },
        };

        var exception = Assert.Throws<InvalidOperationException>(() => _builder.Build(model));
        Assert.Contains("stopaj", exception.Message);
    }

    [Fact]
    public void Invoice_kinds_are_rejected_by_credit_note_builder()
    {
        Assert.False(_builder.CanBuild(DocumentKind.EFatura));
        Assert.False(_builder.CanBuild(DocumentKind.EArsiv));
        Assert.Throws<InvalidOperationException>(() => _builder.Build(TestModels.EArsivSatis()));
    }

    [Fact]
    public void Esmm_seller_is_person_with_tckn()
    {
        var root = RootOf(_builder.Build(TestModels.ESmm()));

        var party = root.Element(Cac + "AccountingSupplierParty")?.Element(Cac + "Party");
        var id = party?.Element(Cac + "PartyIdentification")?.Element(Cbc + "ID");
        Assert.Equal("TCKN", id?.Attribute("schemeID")?.Value);

        var person = party?.Element(Cac + "Person");
        Assert.Equal("Ayşe", person?.Element(Cbc + "FirstName")?.Value);
        Assert.Equal("Yılmaz", person?.Element(Cbc + "FamilyName")?.Value);
    }
}
