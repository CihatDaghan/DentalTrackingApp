using Dental.Application.Reports;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Dental.Infrastructure.Reports;

/// <summary>
/// Hasta kartı "Rapor" sekmesinin antetli A4 çıktıları:
/// tedavi dökümü, durum bildirir rapor ve proforma (fiyat teklifi).
/// Kalıp <c>EpicrisisPdfGenerator</c> ile aynıdır (QuestPDF Community).
/// </summary>
public static class PatientReportPdfGenerator
{
    static PatientReportPdfGenerator() => QuestPDF.Settings.License = LicenseType.Community;

    public static byte[] TreatmentReport(PatientTreatmentReportDto model) =>
        Build(model.ClinicName, "TEDAVİ DÖKÜMÜ", column =>
        {
            var range = model.From is { } f && model.To is { } t
                ? $"{f:dd.MM.yyyy} – {t:dd.MM.yyyy}"
                : "Tüm kayıtlar";

            column.Item().Element(c => IdentityBlock(c,
            [
                ("Hasta", model.PatientName),
                ("Dosya No", model.FileNo),
                ("Dönem", range),
                ("Düzenleme Tarihi", model.IssuedOn.ToString("dd.MM.yyyy")),
            ]));

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(64);
                    columns.RelativeColumn();
                    columns.ConstantColumn(34);
                    columns.ConstantColumn(100);
                    columns.ConstantColumn(60);
                    columns.ConstantColumn(56);
                    columns.ConstantColumn(64);
                });

                table.Header(header =>
                {
                    header.Cell().Element(HeaderCell).Text("Tarih");
                    header.Cell().Element(HeaderCell).Text("Tedavi");
                    header.Cell().Element(HeaderCell).Text("Diş");
                    header.Cell().Element(HeaderCell).Text("Hekim");
                    header.Cell().Element(HeaderCell).AlignRight().Text("Tutar");
                    header.Cell().Element(HeaderCell).AlignRight().Text("İndirim");
                    header.Cell().Element(HeaderCell).AlignRight().Text("Net");
                });

                foreach (var row in model.Rows)
                {
                    table.Cell().Element(Cell).Text(row.Date?.ToString("dd.MM.yyyy") ?? "-").FontSize(9);
                    table.Cell().Element(Cell).Text(row.TreatmentName).FontSize(9);
                    table.Cell().Element(Cell).Text(row.ToothNumber ?? "-").FontSize(9);
                    table.Cell().Element(Cell).Text(row.DoctorName).FontSize(9);
                    table.Cell().Element(Cell).AlignRight().Text(Money(row.Price)).FontSize(9);
                    table.Cell().Element(Cell).AlignRight().Text(Money(row.DiscountAmount)).FontSize(9);
                    table.Cell().Element(Cell).AlignRight().Text(Money(row.NetAmount)).FontSize(9);
                }
            });

            column.Item().AlignRight().Column(c =>
            {
                c.Spacing(2);
                c.Item().Text($"Toplam Tutar: {Money(model.TotalGross)} ₺").FontSize(10);
                c.Item().Text($"Toplam İndirim: {Money(model.TotalDiscount)} ₺").FontSize(10);
                c.Item().Text($"Genel Toplam: {Money(model.TotalNet)} ₺").Bold().FontSize(11);
            });
        });

    public static byte[] StatusReport(PatientStatusReportDto model) =>
        Build(model.ClinicName, "DURUM BİLDİRİR RAPOR", column =>
        {
            column.Item().Element(c => IdentityBlock(c,
            [
                ("Hasta", model.Age is { } age ? $"{model.PatientName} ({age} yaş)" : model.PatientName),
                ("Dosya No", model.FileNo),
                ("Kimlik No", model.IdentityMasked ?? "-"),
                ("Doğum Tarihi", model.BirthDate?.ToString("dd.MM.yyyy") ?? "-"),
                ("Cinsiyet", model.GenderText ?? "-"),
                ("Telefon", model.Phone ?? "-"),
                ("Düzenleme Tarihi", model.IssuedOn.ToString("dd.MM.yyyy")),
            ]));

            column.Item().Column(c =>
            {
                c.Spacing(3);
                c.Item().Text("Mevcut Diş Durumu").Bold().FontSize(11);
                if (model.Teeth.Count == 0)
                {
                    c.Item().Text("Kayıtlı diş durumu bulunmamaktadır.").FontSize(9)
                        .FontColor(Colors.Grey.Darken1);
                }
                else
                {
                    c.Item().Text(string.Join("   ", model.Teeth.Select(t => $"{t.ToothNumber}: {t.ConditionText}")))
                        .FontSize(9);
                }
            });

            column.Item().Column(c =>
            {
                c.Spacing(3);
                c.Item().Text("Yapılan Tedaviler").Bold().FontSize(11);
                if (model.Treatments.Count == 0)
                {
                    c.Item().Text("Dönem içinde yapılmış tedavi kaydı bulunmamaktadır.").FontSize(9)
                        .FontColor(Colors.Grey.Darken1);
                    return;
                }

                c.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(64);
                        columns.RelativeColumn();
                        columns.ConstantColumn(34);
                        columns.ConstantColumn(110);
                    });
                    table.Header(header =>
                    {
                        header.Cell().Element(HeaderCell).Text("Tarih");
                        header.Cell().Element(HeaderCell).Text("Tedavi");
                        header.Cell().Element(HeaderCell).Text("Diş");
                        header.Cell().Element(HeaderCell).Text("Hekim");
                    });
                    foreach (var row in model.Treatments)
                    {
                        table.Cell().Element(Cell).Text(row.Date?.ToString("dd.MM.yyyy") ?? "-").FontSize(9);
                        table.Cell().Element(Cell).Text(row.TreatmentName).FontSize(9);
                        table.Cell().Element(Cell).Text(row.ToothNumber ?? "-").FontSize(9);
                        table.Cell().Element(Cell).Text(row.DoctorName).FontSize(9);
                    }
                });
            });

            column.Item().PaddingTop(20).Element(c => SignatureBlock(c, model.DoctorName, model.DiplomaNo));
        });

    public static byte[] Proforma(ProformaDto model) =>
        Build(model.ClinicName, "FİYAT TEKLİFİ (PROFORMA)", column =>
        {
            column.Item().Element(c => IdentityBlock(c,
            [
                ("Hasta", model.PatientName),
                ("Dosya No", model.FileNo),
                ("Düzenleme Tarihi", model.IssuedOn.ToString("dd.MM.yyyy")),
                ("Geçerlilik Tarihi", model.ValidUntil.ToString("dd.MM.yyyy")),
            ]));

            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(28);
                    columns.RelativeColumn();
                    columns.ConstantColumn(34);
                    columns.ConstantColumn(66);
                    columns.ConstantColumn(56);
                    columns.ConstantColumn(44);
                    columns.ConstantColumn(66);
                });

                table.Header(header =>
                {
                    header.Cell().Element(HeaderCell).Text("#");
                    header.Cell().Element(HeaderCell).Text("Tedavi");
                    header.Cell().Element(HeaderCell).Text("Diş");
                    header.Cell().Element(HeaderCell).AlignRight().Text("Birim Fiyat");
                    header.Cell().Element(HeaderCell).AlignRight().Text("İndirim");
                    header.Cell().Element(HeaderCell).AlignRight().Text("KDV %");
                    header.Cell().Element(HeaderCell).AlignRight().Text("Tutar");
                });

                foreach (var line in model.Lines)
                {
                    table.Cell().Element(Cell).Text(line.SeqNo.ToString()).FontSize(9);
                    table.Cell().Element(Cell).Text(line.TreatmentName).FontSize(9);
                    table.Cell().Element(Cell).Text(line.ToothNumber ?? "-").FontSize(9);
                    table.Cell().Element(Cell).AlignRight().Text(Money(line.UnitPrice)).FontSize(9);
                    table.Cell().Element(Cell).AlignRight().Text(Money(line.DiscountAmount)).FontSize(9);
                    table.Cell().Element(Cell).AlignRight().Text(Money(line.VatRate)).FontSize(9);
                    table.Cell().Element(Cell).AlignRight().Text(Money(line.LineTotal)).FontSize(9);
                }
            });

            column.Item().AlignRight().Column(c =>
            {
                c.Spacing(2);
                c.Item().Text($"Ara Toplam: {Money(model.SubTotal)} ₺").FontSize(10);
                c.Item().Text($"İndirim: {Money(model.DiscountTotal)} ₺").FontSize(10);
                c.Item().Text($"KDV: {Money(model.VatTotal)} ₺").FontSize(10);
                c.Item().Text($"Genel Toplam: {Money(model.GrandTotal)} ₺").Bold().FontSize(12);
            });

            if (!string.IsNullOrWhiteSpace(model.Note))
                column.Item().Text(model.Note).FontSize(9);

            column.Item().PaddingTop(10).Border(1).BorderColor(Colors.Grey.Lighten1).Padding(6)
                .Text(model.Disclaimer).FontSize(8.5f).Bold().FontColor(Colors.Grey.Darken3);
        });

    // ---- Ortak iskelet ----

    private static byte[] Build(string clinicName, string documentTitle, Action<ColumnDescriptor> content) =>
        Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().BorderBottom(1).PaddingBottom(6).Row(row =>
                {
                    row.RelativeItem().Text(clinicName).FontSize(14).Bold();
                    row.ConstantItem(200).AlignRight().Text(documentTitle).FontSize(11).Bold()
                        .FontColor(Colors.Grey.Darken2);
                });

                page.Content().PaddingVertical(10).Column(column =>
                {
                    column.Spacing(10);
                    content(column);
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.DefaultTextStyle(x => x.FontSize(8).FontColor(Colors.Grey.Darken1));
                    text.CurrentPageNumber();
                    text.Span(" / ");
                    text.TotalPages();
                });
            });
        }).GeneratePdf();

    private static void IdentityBlock(IContainer container, IReadOnlyList<(string Label, string Value)> fields) =>
        container.Border(1).BorderColor(Colors.Grey.Lighten1).Padding(8).Column(c =>
        {
            c.Spacing(2);
            foreach (var (label, value) in fields)
            {
                c.Item().Text(text =>
                {
                    text.Span($"{label}: ").Bold();
                    text.Span(value);
                });
            }
        });

    private static void SignatureBlock(IContainer container, string doctorName, string? diplomaNo) =>
        container.AlignRight().Column(c =>
        {
            c.Spacing(2);
            c.Item().AlignRight().Text($"Dr. {doctorName}").Bold();
            if (!string.IsNullOrWhiteSpace(diplomaNo))
                c.Item().AlignRight().Text($"Diploma No: {diplomaNo}").FontSize(8);
            c.Item().AlignRight().Text("İmza / Kaşe").FontSize(7.5f).FontColor(Colors.Grey.Darken1);
            c.Item().Border(1).BorderColor(Colors.Grey.Lighten1).Width(160).Height(56);
        });

    private static IContainer HeaderCell(IContainer container) =>
        container.Border(0.5f).BorderColor(Colors.Grey.Lighten1).Background(Colors.Grey.Lighten4).Padding(4)
            .DefaultTextStyle(x => x.Bold().FontSize(9));

    private static IContainer Cell(IContainer container) =>
        container.Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4);

    private static string Money(decimal value) =>
        value.ToString("#,##0.00", System.Globalization.CultureInfo.GetCultureInfo("tr-TR"));
}
