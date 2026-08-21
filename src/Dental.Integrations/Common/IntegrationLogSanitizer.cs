using System.Text.RegularExpressions;

namespace Dental.Integrations.Common;

/// <summary>
/// Özet loglar (IntegrationCallLogs.Request/ResponseSummary) için PII maskesi:
/// e-posta, telefon ve TCKN serbest metin içinde maskelenir. Tam yükler bu sınıftan geçmez;
/// onlar yasal iz gerektiren kendi tablolarında saklanır.
/// </summary>
public static partial class IntegrationLogSanitizer
{
    // Sıra önemli: önce e-posta (yereldeki rakamlar telefon sanılmasın diye),
    // sonra telefon, en son TCKN (maskelenmiş telefonlar artık 11 hane tutmaz).
    public static string Sanitize(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return "";

        var result = EmailRegex().Replace(text, static m => MaskEmailMatch(m.Value));
        result = PhoneRegex().Replace(result, static m => MaskMiddle(m.Value));
        result = TcknRegex().Replace(result, static m => MaskMiddle(m.Value));
        return result;
    }

    public static string MaskEmail(string email) => MaskEmailMatch(email);

    public static string MaskPhone(string phone) => MaskMiddle(phone);

    public static string MaskTckn(string tckn) => MaskMiddle(tckn);

    /// <summary>İlk 2 ve son 2 karakter kalır, ortası yıldızlanır (örn. 90*******33).</summary>
    private static string MaskMiddle(string value)
        => value.Length <= 4
            ? new string('*', value.Length)
            : value[..2] + new string('*', value.Length - 4) + value[^2..];

    private static string MaskEmailMatch(string email)
    {
        var at = email.IndexOf('@');
        if (at <= 0)
            return "***";
        var local = email[..at];
        var domain = email[(at + 1)..];
        return $"{local[0]}***@{domain[0]}***";
    }

    [GeneratedRegex(@"[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}")]
    private static partial Regex EmailRegex();

    // TR telefon biçimleri: +905XXXXXXXXX, 905XXXXXXXXX, 05XXXXXXXXX, 5XXXXXXXXX.
    [GeneratedRegex(@"(?<!\d)(?:\+?90|0)?5\d{9}(?!\d)")]
    private static partial Regex PhoneRegex();

    // TCKN: 11 hane, ilk hane 0 olamaz.
    [GeneratedRegex(@"(?<!\d)[1-9]\d{10}(?!\d)")]
    private static partial Regex TcknRegex();
}
