namespace Dental.Application.Common;

/// <summary>TCKN checksum doğrulaması: 11 hane, ilk hane 0 değil, 10. ve 11. hane algoritması.</summary>
public static class TcknValidator
{
    public static bool IsValid(string? tckn)
    {
        if (string.IsNullOrWhiteSpace(tckn) || tckn.Length != 11 || tckn[0] == '0')
            return false;
        if (!tckn.All(char.IsAsciiDigit))
            return false;

        var d = tckn.Select(c => c - '0').ToArray();
        var oddSum = d[0] + d[2] + d[4] + d[6] + d[8];
        var evenSum = d[1] + d[3] + d[5] + d[7];
        var digit10 = ((oddSum * 7 - evenSum) % 10 + 10) % 10;
        var digit11 = d.Take(10).Sum() % 10;
        return d[9] == digit10 && d[10] == digit11;
    }
}
