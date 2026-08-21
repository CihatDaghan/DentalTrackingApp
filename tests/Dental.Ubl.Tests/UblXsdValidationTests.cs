using System.Xml;
using System.Xml.Linq;
using System.Xml.Schema;
using Dental.EDocument.Ubl.Builders;
using Dental.EDocument.Ubl.Models;

namespace Dental.Ubl.Tests;

/// <summary>
/// OASIS UBL 2.1 şema seti (Schemas/ altında; maindoc + common) bir kez derlenir.
/// Not: UBL-xmldsig-core-schema-2.1.xsd kopyasından inert DOCTYPE bloğu çıkarılmıştır
/// (XmlSchemaSet DTD kabul etmez; tanımlı entity'ler şema gövdesinde kullanılmıyordu).
/// </summary>
public sealed class UblSchemaFixture
{
    public UblSchemaFixture()
    {
        var schemaRoot = Path.Combine(AppContext.BaseDirectory, "Schemas");
        var invoiceXsd = Path.Combine(schemaRoot, "maindoc", "UBL-Invoice-2.1.xsd");
        var creditNoteXsd = Path.Combine(schemaRoot, "maindoc", "UBL-CreditNote-2.1.xsd");

        if (!File.Exists(invoiceXsd) || !File.Exists(creditNoteXsd))
        {
            throw new FileNotFoundException(
                $"UBL 2.1 şemaları bulunamadı: {schemaRoot}. " +
                "Schemas/ klasörünün test çıktısına kopyalandığından emin olun.");
        }

        var schemaSet = new XmlSchemaSet { XmlResolver = new XmlUrlResolver() };
        schemaSet.Add("urn:oasis:names:specification:ubl:schema:xsd:Invoice-2", invoiceXsd);
        schemaSet.Add("urn:oasis:names:specification:ubl:schema:xsd:CreditNote-2", creditNoteXsd);
        schemaSet.Compile();
        SchemaSet = schemaSet;
    }

    public XmlSchemaSet SchemaSet { get; }
}

[CollectionDefinition("UblSchemas")]
public sealed class UblSchemaCollection : ICollectionFixture<UblSchemaFixture>;

[Collection("UblSchemas")]
public sealed class UblXsdValidationTests(UblSchemaFixture fixture)
{
    private readonly InvoiceUblBuilder _invoiceBuilder = new();
    private readonly CreditNoteUblBuilder _creditNoteBuilder = new();

    private IReadOnlyList<string> Validate(XDocument document)
    {
        var errors = new List<string>();
        document.Validate(fixture.SchemaSet,
            (_, e) => errors.Add($"[{e.Severity}] {e.Message}"));
        return errors;
    }

    private void AssertValid(XDocument document)
    {
        var errors = Validate(document);
        Assert.True(errors.Count == 0,
            $"XSD doğrulama hataları:{Environment.NewLine}{string.Join(Environment.NewLine, errors)}");
    }

    [Fact]
    public void Earsiv_satis_invoice_is_schema_valid()
        => AssertValid(_invoiceBuilder.Build(TestModels.EArsivSatis()));

    [Fact]
    public void Efatura_satis_invoice_is_schema_valid()
        => AssertValid(_invoiceBuilder.Build(TestModels.EFaturaSatis()));

    [Fact]
    public void Tevkifat_invoice_with_withholding_is_schema_valid()
        => AssertValid(_invoiceBuilder.Build(TestModels.EFaturaTevkifat()));

    [Fact]
    public void Istisna_invoice_for_foreign_patient_is_schema_valid()
        => AssertValid(_invoiceBuilder.Build(TestModels.EArsivIstisna()));

    [Fact]
    public void Iade_invoice_with_billing_reference_is_schema_valid()
        => AssertValid(_invoiceBuilder.Build(TestModels.EArsivIade()));

    [Fact]
    public void Esmm_credit_note_with_stopaj_is_schema_valid()
        => AssertValid(_creditNoteBuilder.Build(TestModels.ESmm(vatRegisteredBuyer: true)));

    [Fact]
    public void Esmm_credit_note_without_stopaj_is_schema_valid()
        => AssertValid(_creditNoteBuilder.Build(TestModels.ESmm(vatRegisteredBuyer: false)));

    [Fact]
    public void Esmm_validates_against_credit_note_schema_not_invoice()
    {
        // Yapısal koruma: e-SMM kökü CreditNote şemasından çözülmeli.
        var document = _creditNoteBuilder.Build(TestModels.ESmm());
        var rootNamespace = document.Root!.Name.NamespaceName;

        Assert.Equal("urn:oasis:names:specification:ubl:schema:xsd:CreditNote-2", rootNamespace);
        AssertValid(document);
    }
}
