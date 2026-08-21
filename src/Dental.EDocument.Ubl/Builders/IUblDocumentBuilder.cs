using System.Text;
using System.Xml.Linq;
using Dental.EDocument.Ubl.Models;

namespace Dental.EDocument.Ubl.Builders;

public interface IUblDocumentBuilder
{
    bool CanBuild(DocumentKind kind);

    XDocument Build(EDocumentModel model);
}

public static class UblDocumentBuilderExtensions
{
    /// <summary>UTF-8 bildirimli XML metni üretir.</summary>
    public static string BuildXmlString(this IUblDocumentBuilder builder, EDocumentModel model)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var document = builder.Build(model);
        var output = new StringBuilder();
        using (var writer = new Utf8StringWriter(output))
        {
            document.Save(writer);
        }

        return output.ToString();
    }

    private sealed class Utf8StringWriter(StringBuilder sb) : StringWriter(sb)
    {
        public override Encoding Encoding => Encoding.UTF8;
    }
}
