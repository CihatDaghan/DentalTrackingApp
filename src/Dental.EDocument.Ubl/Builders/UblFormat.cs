using System.Globalization;

namespace Dental.EDocument.Ubl.Builders;

/// <summary>Sayı/tarih biçimleri: InvariantCulture, ondalık nokta.</summary>
internal static class UblFormat
{
    /// <summary>Parasal tutar — daima 2 hane.</summary>
    public static string Amount(decimal value) => value.ToString("F2", CultureInfo.InvariantCulture);

    public static string Quantity(decimal value) => value.ToString("0.####", CultureInfo.InvariantCulture);

    public static string Percent(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    public static string Rate(decimal value) => value.ToString("0.######", CultureInfo.InvariantCulture);

    public static string Date(DateOnly value) => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string Time(TimeOnly value) => value.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
}
