using System.Reflection;
using System.Text;

namespace Dental.EDocument.Ubl;

/// <summary>
/// Gömülü varsayılan XSLT şablonu. GİB e-belgelerinde görüntüleme şablonunun
/// AdditionalDocumentReference içinde base64 gömülmesi zorunludur.
/// </summary>
public static class EmbeddedXslt
{
    private const string ResourceName = "Dental.EDocument.Ubl.Resources.DefaultDocument.xslt";

    private static readonly Lazy<string> Content = new(Load);

    public static string GetXslt() => Content.Value;

    public static string GetBase64() => Convert.ToBase64String(Encoding.UTF8.GetBytes(Content.Value));

    private static string Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Embedded resource not found: {ResourceName}");
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
