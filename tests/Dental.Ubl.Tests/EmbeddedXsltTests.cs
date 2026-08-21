using System.Xml;
using System.Xml.Xsl;
using Dental.EDocument.Ubl;
using Dental.EDocument.Ubl.Builders;

namespace Dental.Ubl.Tests;

public sealed class EmbeddedXsltTests
{
    [Fact]
    public void Embedded_xslt_resource_loads()
    {
        var xslt = EmbeddedXslt.GetXslt();

        Assert.Contains("<xsl:stylesheet", xslt);
        Assert.NotEmpty(EmbeddedXslt.GetBase64());
    }

    [Fact]
    public void Embedded_xslt_compiles_and_transforms_an_invoice()
    {
        var transform = new XslCompiledTransform();
        using (var reader = XmlReader.Create(new StringReader(EmbeddedXslt.GetXslt())))
        {
            transform.Load(reader);
        }

        var document = new InvoiceUblBuilder().Build(TestModels.EArsivSatis());
        using var input = document.CreateReader();
        using var output = new StringWriter();
        transform.Transform(input, null, output);

        var html = output.ToString();
        Assert.Contains("DIS2026000000001", html);
        Assert.Contains("İmplant uygulaması", html);
    }
}
