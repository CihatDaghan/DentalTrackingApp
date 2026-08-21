namespace Dental.Domain.Common;

/// <summary>
/// FDI (ISO 3950) diş numaralandırması: kalıcı dişler 11-18/21-28/31-38/41-48,
/// süt dişleri 51-55/61-65/71-75/81-85. Tüm diş no doğrulamaları buradan.
/// </summary>
public static class FdiTeeth
{
    /// <summary>Kalıcı dişler — kadran 1-4, diş 1-8.</summary>
    public static readonly IReadOnlyList<string> Permanent =
        [.. from quadrant in new[] { 1, 2, 3, 4 }
            from position in Enumerable.Range(1, 8)
            select $"{quadrant}{position}"];

    /// <summary>Süt dişleri — kadran 5-8, diş 1-5.</summary>
    public static readonly IReadOnlyList<string> Deciduous =
        [.. from quadrant in new[] { 5, 6, 7, 8 }
            from position in Enumerable.Range(1, 5)
            select $"{quadrant}{position}"];

    public static readonly IReadOnlySet<string> All = new HashSet<string>([.. Permanent, .. Deciduous]);

    public static bool IsValid(string? toothNumber) =>
        toothNumber is not null && All.Contains(toothNumber.Trim());

    public static bool IsDeciduous(string toothNumber) => toothNumber.Trim() is ['5' or '6' or '7' or '8', _];

    /// <summary>Üst çene: kadran 1, 2 (kalıcı) ve 5, 6 (süt).</summary>
    public static bool IsUpperJaw(string toothNumber) => toothNumber.Trim() is ['1' or '2' or '5' or '6', _];
}
