using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Dental.Infrastructure.Prescriptions;

public sealed record PrescriptionPdfItem(
    string DrugName,
    string? Form,
    int BoxCount,
    string? Dose,
    string? Usage,
    bool IsControlled);

public sealed record PrescriptionPdfModel(
    string ClinicName,
    string DoctorName,
    string? DiplomaNo,
    string PatientName,
    int? PatientAge,
    string PrescriptionNo,
    DateTime DateLocal,
    IReadOnlyList<PrescriptionPdfItem> Items);

/// <summary>
/// A5 reçete çıktısı: klinik antet, reçete no/tarih, hasta bilgisi, ilaç listesi,
/// hekim adı + diploma no + imza alanı. Kontrole tabi ilaç varsa uyarı bandı basılır
/// (Renkli Reçete sistemine yönlendirme — entegrasyon yok). QuestPDF Community lisansı
/// ConsentPdfGenerator ile aynı koşulda statik ayarlanır.
/// </summary>
public static class PrescriptionPdfGenerator
{
    static PrescriptionPdfGenerator() => QuestPDF.Settings.License = LicenseType.Community;

    public static byte[] Generate(PrescriptionPdfModel model)
    {
        var hasControlled = model.Items.Any(i => i.IsControlled);

        return Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A5);
                page.Margin(28);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Column(column =>
                {
                    column.Item().BorderBottom(1).PaddingBottom(4).Row(row =>
                    {
                        row.RelativeItem().Text(model.ClinicName).FontSize(13).Bold();
                        row.ConstantItem(110).AlignRight().Text("REÇETE").FontSize(11).Bold()
                            .FontColor(Colors.Grey.Darken2);
                    });
                    column.Item().PaddingTop(4).Row(row =>
                    {
                        row.RelativeItem().Text($"Reçete No: {model.PrescriptionNo}").Bold();
                        row.ConstantItem(110).AlignRight().Text($"Tarih: {model.DateLocal:dd.MM.yyyy}");
                    });
                    column.Item().Text(model.PatientAge is { } age
                        ? $"Hasta: {model.PatientName} ({age} yaş)"
                        : $"Hasta: {model.PatientName}");
                });

                page.Content().PaddingVertical(8).Column(column =>
                {
                    column.Spacing(6);

                    if (hasControlled)
                    {
                        column.Item().Background(Colors.Red.Lighten4).Border(1)
                            .BorderColor(Colors.Red.Medium).Padding(6)
                            .Text("Kontrole tabi ilaç içerir — Renkli Reçete sistemi üzerinden yazılması gerekir.")
                            .FontColor(Colors.Red.Darken2).Bold().FontSize(8.5f);
                    }

                    var index = 0;
                    foreach (var item in model.Items)
                    {
                        column.Item().Column(c =>
                        {
                            c.Item().Text(text =>
                            {
                                text.Span($"{++index}. ");
                                text.Span(item.DrugName).Bold();
                                if (!string.IsNullOrWhiteSpace(item.Form))
                                    text.Span($" ({item.Form})");
                                text.Span($"  ×{item.BoxCount} kutu");
                                if (item.IsControlled)
                                    text.Span("  [KONTROLE TABİ]").FontColor(Colors.Red.Darken2).Bold();
                            });
                            var usage = string.Join(" — ", new[] { item.Dose, item.Usage }
                                .Where(s => !string.IsNullOrWhiteSpace(s)));
                            if (usage.Length > 0)
                                c.Item().PaddingLeft(14).Text($"S: {usage}").FontColor(Colors.Grey.Darken2);
                        });
                    }
                });

                page.Footer().Column(column =>
                {
                    column.Item().BorderTop(1).PaddingTop(6).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text($"Dr. {model.DoctorName}").Bold();
                            if (!string.IsNullOrWhiteSpace(model.DiplomaNo))
                                c.Item().Text($"Diploma No: {model.DiplomaNo}").FontSize(8);
                        });
                        row.ConstantItem(130).Column(c =>
                        {
                            c.Item().Text("İmza / Kaşe").FontSize(7.5f).FontColor(Colors.Grey.Darken1);
                            c.Item().Border(1).BorderColor(Colors.Grey.Lighten1).Height(48);
                        });
                    });
                });
            });
        }).GeneratePdf();
    }
}
