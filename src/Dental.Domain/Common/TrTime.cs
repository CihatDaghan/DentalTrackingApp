namespace Dental.Domain.Common;

/// <summary>
/// Türkiye saat dilimi yardımcıları. TR, 2016'dan beri sabit UTC+3 (DST yok).
/// Finansal "gün" sınırları yerel (Europe/Istanbul) gündür; sorgular UTC aralığına çevrilir.
/// </summary>
public static class TrTime
{
    public static readonly TimeSpan Offset = TimeSpan.FromHours(3);

    /// <summary>UTC andan Türkiye yerel tarihini üretir.</summary>
    public static DateOnly ToLocalDate(DateTime utc) => DateOnly.FromDateTime(utc + Offset);

    /// <summary>Yerel (TR) günün [başlangıç, bitiş) UTC aralığı.</summary>
    public static (DateTime StartUtc, DateTime EndUtc) DayRangeUtc(DateOnly localDate)
    {
        var startUtc = localDate.ToDateTime(TimeOnly.MinValue) - Offset;
        return (startUtc, startUtc.AddDays(1));
    }
}
