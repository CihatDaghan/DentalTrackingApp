using System.Text;

namespace Dental.Infrastructure.Messaging;

/// <summary>Şablon yer tutucu doldurma: {anahtar} → değer. Bilinmeyen yer tutucular olduğu gibi bırakılır.</summary>
public static class MessageRenderer
{
    public static string Render(string body, IReadOnlyDictionary<string, string>? values)
    {
        if (values is null || values.Count == 0 || !body.Contains('{')) return body;

        var sb = new StringBuilder(body);
        foreach (var (key, value) in values)
            sb.Replace("{" + key + "}", value ?? "");
        return sb.ToString();
    }
}

/// <summary>
/// TR telefon normalizasyonu. Saklama biçimi E.164'tür (+905XXXXXXXXX); sürücülere
/// '+' olmadan (905XXXXXXXXX) verilir — Netgsm/Meta bu biçimi bekler.
/// </summary>
public static class PhoneNumbers
{
    /// <summary>Normalize edemezse null döner (kayıt Skipped/InvalidNumber olur).</summary>
    public static string? NormalizeTr(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var digits = new StringBuilder(raw.Length);
        foreach (var c in raw)
            if (char.IsAsciiDigit(c)) digits.Append(c);

        var value = digits.ToString();
        // 00 ile başlayan uluslararası önek at.
        if (value.StartsWith("00", StringComparison.Ordinal)) value = value[2..];
        // Şehirlerarası '0' öneki (05XX...) at.
        if (value.Length == 11 && value.StartsWith('0')) value = value[1..];
        // Ülke kodu yoksa TR varsayılır.
        if (value.Length == 10 && value.StartsWith('5')) value = "90" + value;

        // TR cep numarası: 90 + 5XXXXXXXXX = 12 hane.
        return value.Length == 12 && value.StartsWith("905", StringComparison.Ordinal) ? "+" + value : null;
    }

    /// <summary>Sürücü biçimi: baştaki '+' atılır.</summary>
    public static string ToProviderFormat(string e164) => e164.TrimStart('+');
}
