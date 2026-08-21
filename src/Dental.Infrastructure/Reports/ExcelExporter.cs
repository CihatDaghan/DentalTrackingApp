using ClosedXML.Excel;

namespace Dental.Infrastructure.Reports;

/// <summary>Hücre biçimi — TR sayı/tarih biçimleri tek yerde tanımlanır.</summary>
public enum ExcelValueKind : byte
{
    Text = 0,
    Integer = 1,
    Money = 2,
    Percent = 3,
    Date = 4,
    DateTime = 5,
}

public sealed record ExcelColumn(string Header, ExcelValueKind Kind = ExcelValueKind.Text, double Width = 0);

public sealed record ExcelSheet(string Name, IReadOnlyList<ExcelColumn> Columns, IReadOnlyList<object?[]> Rows);

/// <summary>
/// Jenerik Excel üreticisi: başlık satırı + veri. Her rapor kendi sayfa tanımını verir,
/// biçimlendirme (TR tarih/para/oran) burada tek yerde uygulanır.
/// </summary>
public static class ExcelExporter
{
    private const string MoneyFormat = "#,##0.00";
    private const string IntegerFormat = "#,##0";
    private const string PercentFormat = "0.0%";
    private const string DateFormat = "dd.MM.yyyy";
    private const string DateTimeFormat = "dd.MM.yyyy HH:mm";

    public static byte[] Build(IReadOnlyList<ExcelSheet> sheets)
    {
        using var workbook = new XLWorkbook();
        foreach (var sheet in sheets)
        {
            // Excel sayfa adı 31 karakterle sınırlıdır ve : \ / ? * [ ] içeremez.
            var worksheet = workbook.Worksheets.Add(SafeSheetName(sheet.Name));

            for (var c = 0; c < sheet.Columns.Count; c++)
            {
                var cell = worksheet.Cell(1, c + 1);
                cell.Value = sheet.Columns[c].Header;
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromArgb(0xEF, 0xF3, 0xF8);
                cell.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
            }

            for (var r = 0; r < sheet.Rows.Count; r++)
            {
                var row = sheet.Rows[r];
                for (var c = 0; c < sheet.Columns.Count && c < row.Length; c++)
                    WriteCell(worksheet.Cell(r + 2, c + 1), row[c], sheet.Columns[c].Kind);
            }

            for (var c = 0; c < sheet.Columns.Count; c++)
            {
                if (sheet.Columns[c].Width > 0) worksheet.Column(c + 1).Width = sheet.Columns[c].Width;
                else worksheet.Column(c + 1).AdjustToContents(1, Math.Min(sheet.Rows.Count + 1, 200));
            }

            if (sheet.Rows.Count > 0)
                worksheet.SheetView.FreezeRows(1);
        }

        // Hiç sayfa yoksa ClosedXML kaydetmeyi reddeder.
        if (workbook.Worksheets.Count == 0) workbook.Worksheets.Add("Bos");

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static void WriteCell(IXLCell cell, object? value, ExcelValueKind kind)
    {
        if (value is null) return;

        switch (kind)
        {
            case ExcelValueKind.Integer:
                cell.Value = Convert.ToDouble(value);
                cell.Style.NumberFormat.Format = IntegerFormat;
                break;
            case ExcelValueKind.Money:
                cell.Value = Convert.ToDouble(value);
                cell.Style.NumberFormat.Format = MoneyFormat;
                break;
            case ExcelValueKind.Percent:
                // Oranlar DTO'da yüzde birimindedir (12.5 = %12,5); Excel 0-1 aralığı bekler.
                cell.Value = Convert.ToDouble(value) / 100d;
                cell.Style.NumberFormat.Format = PercentFormat;
                break;
            case ExcelValueKind.Date:
                cell.Value = value is DateOnly d ? d.ToDateTime(TimeOnly.MinValue) : Convert.ToDateTime(value);
                cell.Style.NumberFormat.Format = DateFormat;
                break;
            case ExcelValueKind.DateTime:
                cell.Value = Convert.ToDateTime(value);
                cell.Style.NumberFormat.Format = DateTimeFormat;
                break;
            default:
                cell.Value = value.ToString();
                break;
        }
    }

    private static string SafeSheetName(string name)
    {
        var cleaned = new string([.. name.Where(c => c is not (':' or '\\' or '/' or '?' or '*' or '[' or ']'))]);
        return cleaned.Length <= 31 ? cleaned : cleaned[..31];
    }
}
