using Ganss.Xss;

namespace Dental.Infrastructure.Consents;

/// <summary>
/// Onam HTML'i sunucu tarafı sanitizasyonu (denetim (c)-7): şablon kaydında ve render'da uygulanır.
/// İzinli etiketler: p, br, h1-h3, strong, b, em, i, u, ul, ol, li, span.
/// style/script/iframe/on* ve tüm öznitelik/şema/CSS'ler yasaktır.
/// </summary>
public static class ConsentHtml
{
    private static readonly HtmlSanitizer Sanitizer = Create();

    private static HtmlSanitizer Create()
    {
        var sanitizer = new HtmlSanitizer(new HtmlSanitizerOptions
        {
            AllowedTags = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "p", "br", "h1", "h2", "h3", "strong", "b", "em", "i", "u", "ul", "ol", "li", "span" },
            AllowedAttributes = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            AllowedCssProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            AllowedAtRules = new HashSet<AngleSharp.Css.Dom.CssRuleType>(),
            AllowedSchemes = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
            UriAttributes = new HashSet<string>(StringComparer.OrdinalIgnoreCase),
        });
        // KeepChildNodes varsayılanı (false): izinsiz etiket İÇERİĞİYLE atılır — script gövdesi
        // metin olarak sızmaz. Editör çıktısı zaten izinli blok etiketleriyle sınırlıdır.
        return sanitizer;
    }

    public static string Sanitize(string html) => Sanitizer.Sanitize(html ?? "");

    /// <summary>Yer tutucu değerleri HTML'e gömülmeden önce kaçışlanır (hasta adı işaretleme içeremez).</summary>
    public static string Encode(string? value) => System.Net.WebUtility.HtmlEncode(value ?? "");
}
