using System.Collections.Frozen;
using System.Globalization;

namespace Dental.Infrastructure.Invoices;

/// <summary>
/// Uyruk kodu dönüşümü. Hasta kartındaki uyruk SKRS'ye uygun alfa-3'tür (TUR, DEU, GBR...);
/// UBL-TR ise <c>cac:Person/cbc:NationalityID</c> alanında ISO 3166-1 <b>alpha-2</b> bekler
/// (GİB e-Fatura Paketi schematron'u değeri <c>$CountryCodeList</c> ile doğrular: TR, DE, GB...).
/// Tablo .NET'in kendi bölge verisinden üretilir; elle kod listesi taşınmaz.
/// </summary>
public static class NationalityCodes
{
    private static readonly FrozenDictionary<string, string> Alpha3ToAlpha2 = BuildMap();

    /// <summary>
    /// Alfa-3 uyruk kodunu ISO alpha-2'ye çevirir. Girdi zaten 2 harfliyse olduğu gibi döner;
    /// tanınmayan kodda <c>null</c> döner (çağıran uyarı üretir, alan boş bırakılır —
    /// geçersiz bir kod göndermek belgenin schematron'da reddedilmesine yol açardı).
    /// </summary>
    public static string? ToAlpha2(string? alpha3)
    {
        if (string.IsNullOrWhiteSpace(alpha3)) return null;
        var code = alpha3.Trim().ToUpperInvariant();
        if (code.Length == 2) return code;
        return Alpha3ToAlpha2.TryGetValue(code, out var alpha2) ? alpha2 : null;
    }

    private static FrozenDictionary<string, string> BuildMap()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var culture in CultureInfo.GetCultures(CultureTypes.SpecificCultures))
        {
            try
            {
                var region = new RegionInfo(culture.Name);
                map.TryAdd(region.ThreeLetterISORegionName, region.TwoLetterISORegionName);
            }
            catch (ArgumentException)
            {
                // Bölge bilgisi olmayan kültürler (ör. yapay/uydurma adlar) atlanır.
            }
        }

        return map.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }
}
