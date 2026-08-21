using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Dental.Infrastructure.Epicrisis;

public sealed record EpicrisisPdfDiagnosis(string Code, string Name);

public sealed record EpicrisisPdfTreatment(DateOnly? Date, string? ToothNumber, string Name, string DoctorName);

public sealed record EpicrisisPdfModel(
    string ClinicName,
    string Title,
    string PatientName,
    string? FileNo,
    DateOnly? BirthDate,
    int? PatientAge,
    string? GenderText,
    string DoctorName,
    string? DiplomaNo,
    DateTime DateLocal,
    IReadOnlyList<EpicrisisPdfDiagnosis> Diagnoses,
    IReadOnlyList<EpicrisisPdfTreatment> Treatments,
    string? BodyText);

/// <summary>
/// Antetli A4 epikriz: hasta kimlik bloğu, ICD tanı listesi, tedavi tablosu
/// (tarih/diş/işlem/hekim), sonuç-öneri metni ve hekim imza alanı.
/// </summary>
public static class EpicrisisPdfGenerator
{
    static EpicrisisPdfGenerator() => QuestPDF.Settings.License = LicenseType.Community;

    public static byte[] Generate(EpicrisisPdfModel model)
    {
        return Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().Column(column =>
                {
                    column.Item().BorderBottom(1).PaddingBottom(6).Row(row =>
                    {
                        row.RelativeItem().Text(model.ClinicName).FontSize(14).Bold();
                        row.ConstantItem(160).AlignRight().Text("EPİKRİZ").FontSize(12).Bold()
                            .FontColor(Colors.Grey.Darken2);
                    });
                });

                page.Content().PaddingVertical(10).Column(column =>
                {
                    column.Spacing(10);

                    column.Item().Text(model.Title).FontSize(12).Bold();

                    // Hasta kimlik bloğu
                    column.Item().Border(1).BorderColor(Colors.Grey.Lighten1).Padding(8).Column(c =>
                    {
                        c.Spacing(2);
                        c.Item().Text(text =>
                        {
                            text.Span("Hasta: ").Bold();
                            text.Span(model.PatientAge is { } age
                                ? $"{model.PatientName} ({age} yaş)"
                                : model.PatientName);
                        });
                        if (!string.IsNullOrWhiteSpace(model.FileNo))
                            c.Item().Text(text => { text.Span("Dosya No: ").Bold(); text.Span(model.FileNo); });
                        if (model.BirthDate is { } birth)
                            c.Item().Text(text =>
                            {
                                text.Span("Doğum Tarihi: ").Bold();
                                text.Span(birth.ToString("dd.MM.yyyy"));
                            });
                        if (!string.IsNullOrWhiteSpace(model.GenderText))
                            c.Item().Text(text => { text.Span("Cinsiyet: ").Bold(); text.Span(model.GenderText); });
                        c.Item().Text(text =>
                        {
                            text.Span("Düzenleme Tarihi: ").Bold();
                            text.Span(model.DateLocal.ToString("dd.MM.yyyy"));
                        });
                    });

                    // Tanılar
                    if (model.Diagnoses.Count > 0)
                    {
                        column.Item().Column(c =>
                        {
                            c.Spacing(2);
                            c.Item().Text("Tanılar (ICD-10)").Bold().FontSize(11);
                            foreach (var d in model.Diagnoses)
                                c.Item().PaddingLeft(10).Text($"• {d.Code} — {d.Name}");
                        });
                    }

                    // Tedavi tablosu
                    if (model.Treatments.Count > 0)
                    {
                        column.Item().Column(c =>
                        {
                            c.Spacing(2);
                            c.Item().Text("Uygulanan Tedaviler").Bold().FontSize(11);
                            c.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.ConstantColumn(70);
                                    columns.ConstantColumn(40);
                                    columns.RelativeColumn();
                                    columns.ConstantColumn(120);
                                });

                                static QuestPDF.Infrastructure.IContainer Cell(QuestPDF.Infrastructure.IContainer c) =>
                                    c.Border(0.5f).BorderColor(Colors.Grey.Lighten1).Padding(4);

                                table.Header(header =>
                                {
                                    header.Cell().Element(Cell).Text("Tarih").Bold().FontSize(9);
                                    header.Cell().Element(Cell).Text("Diş").Bold().FontSize(9);
                                    header.Cell().Element(Cell).Text("İşlem").Bold().FontSize(9);
                                    header.Cell().Element(Cell).Text("Hekim").Bold().FontSize(9);
                                });

                                foreach (var t in model.Treatments)
                                {
                                    table.Cell().Element(Cell).Text(t.Date?.ToString("dd.MM.yyyy") ?? "-").FontSize(9);
                                    table.Cell().Element(Cell).Text(t.ToothNumber ?? "-").FontSize(9);
                                    table.Cell().Element(Cell).Text(t.Name).FontSize(9);
                                    table.Cell().Element(Cell).Text(t.DoctorName).FontSize(9);
                                }
                            });
                        });
                    }

                    // Sonuç / öneriler
                    if (!string.IsNullOrWhiteSpace(model.BodyText))
                    {
                        column.Item().Column(c =>
                        {
                            c.Spacing(2);
                            c.Item().Text("Sonuç ve Öneriler").Bold().FontSize(11);
                            c.Item().Text(model.BodyText);
                        });
                    }

                    // Hekim imza alanı
                    column.Item().PaddingTop(24).AlignRight().Column(c =>
                    {
                        c.Spacing(2);
                        c.Item().AlignRight().Text($"Dr. {model.DoctorName}").Bold();
                        if (!string.IsNullOrWhiteSpace(model.DiplomaNo))
                            c.Item().AlignRight().Text($"Diploma No: {model.DiplomaNo}").FontSize(8);
                        c.Item().AlignRight().Text("İmza / Kaşe").FontSize(7.5f).FontColor(Colors.Grey.Darken1);
                        c.Item().Border(1).BorderColor(Colors.Grey.Lighten1).Width(160).Height(56);
                    });
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
    }
}
