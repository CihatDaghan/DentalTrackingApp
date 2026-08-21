using System.Text.RegularExpressions;
using AngleSharp.Html.Parser;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
// AngleSharp.Dom, QuestPDF ile (Document/IElement) çakışır — DOM türleri takma adla alınır.
using IDomElement = AngleSharp.Dom.IElement;
using IDomNode = AngleSharp.Dom.INode;
using IDomText = AngleSharp.Dom.IText;

namespace Dental.Infrastructure.Consents;

public sealed record ConsentPdfModel(
    string ClinicName,
    string RenderedHtml,
    string SignerName,
    DateTime SignedAtLocal,
    string? SignerIp,
    byte[] SignaturePng);

/// <summary>
/// Sanitize edilmiş sınırlı onam HTML'ini (p/h1-h3/strong/em/u/ul/ol/li/br/span) QuestPDF
/// öğelerine çeviren mini dönüştürücü + imza bloğu ve klinik antetiyle nihai PDF üretimi.
/// QuestPDF Community lisansı burada statik olarak ayarlanır (yıllık gelir &lt; 1M$ koşulu —
/// ticari büyümede lisans kalemi risk listesindedir).
/// </summary>
public static class ConsentPdfGenerator
{
    static ConsentPdfGenerator() => QuestPDF.Settings.License = LicenseType.Community;

    public static byte[] Generate(ConsentPdfModel model)
    {
        var blocks = ParseBlocks(model.RenderedHtml);

        return Document.Create(document =>
        {
            document.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(36);
                page.DefaultTextStyle(x => x.FontSize(10));

                page.Header().BorderBottom(1).PaddingBottom(6)
                    .Text(model.ClinicName).FontSize(14).Bold();

                page.Content().PaddingVertical(10).Column(column =>
                {
                    column.Spacing(6);
                    foreach (var block in blocks)
                        RenderBlock(column, block);

                    column.Item().PaddingTop(24).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Spacing(2);
                            c.Item().Text($"İmzalayan: {model.SignerName}").Bold();
                            c.Item().Text($"Tarih: {model.SignedAtLocal:dd.MM.yyyy HH:mm}");
                            if (!string.IsNullOrWhiteSpace(model.SignerIp))
                                c.Item().Text($"IP: {model.SignerIp}");
                        });
                        row.ConstantItem(180).Column(c =>
                        {
                            c.Item().Text("İmza").FontSize(8).FontColor(Colors.Grey.Darken1);
                            c.Item().Border(1).BorderColor(Colors.Grey.Lighten1).Padding(4).Height(70)
                                .AlignCenter().AlignMiddle().Image(model.SignaturePng).FitArea();
                        });
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

    // ---- Mini HTML → QuestPDF dönüştürücü ----

    private sealed record TextRun(string Text, bool Bold, bool Italic, bool Underline);

    private abstract record Block;
    private sealed record ParagraphBlock(IReadOnlyList<TextRun> Runs) : Block;
    private sealed record HeadingBlock(int Level, IReadOnlyList<TextRun> Runs) : Block;
    private sealed record ListBlock(bool Ordered, IReadOnlyList<IReadOnlyList<TextRun>> Items) : Block;

    private static List<Block> ParseBlocks(string html)
    {
        var document = new HtmlParser().ParseDocument($"<body>{html}</body>");
        var blocks = new List<Block>();
        var looseRuns = new List<TextRun>();

        void FlushLoose()
        {
            if (looseRuns.Count == 0) return;
            blocks.Add(new ParagraphBlock([.. looseRuns]));
            looseRuns = [];
        }

        foreach (var node in document.Body!.ChildNodes)
        {
            switch (node)
            {
                case IDomText text when !string.IsNullOrWhiteSpace(text.Data):
                    looseRuns.Add(new TextRun(Normalize(text.Data), false, false, false));
                    break;
                case IDomElement el:
                    var tag = el.TagName.ToLowerInvariant();
                    switch (tag)
                    {
                        case "h1" or "h2" or "h3":
                            FlushLoose();
                            blocks.Add(new HeadingBlock(tag[1] - '0', CollectRuns(el)));
                            break;
                        case "p":
                            FlushLoose();
                            blocks.Add(new ParagraphBlock(CollectRuns(el)));
                            break;
                        case "ul" or "ol":
                            FlushLoose();
                            var items = el.Children
                                .Where(c => c.TagName.Equals("li", StringComparison.OrdinalIgnoreCase))
                                .Select(li => (IReadOnlyList<TextRun>)CollectRuns(li))
                                .ToList();
                            blocks.Add(new ListBlock(tag == "ol", items));
                            break;
                        case "br":
                            looseRuns.Add(new TextRun("\n", false, false, false));
                            break;
                        default: // span, strong vb. blok dışı kullanım → gevşek satıra katılır
                            looseRuns.AddRange(CollectRuns(el));
                            break;
                    }
                    break;
            }
        }
        FlushLoose();
        return blocks;
    }

    private static List<TextRun> CollectRuns(IDomNode parent) =>
        CollectRuns(parent, bold: false, italic: false, underline: false);

    private static List<TextRun> CollectRuns(IDomNode parent, bool bold, bool italic, bool underline)
    {
        var runs = new List<TextRun>();
        foreach (var node in parent.ChildNodes)
        {
            switch (node)
            {
                case IDomText text when text.Data.Length > 0:
                    var normalized = Normalize(text.Data);
                    if (normalized.Length > 0) runs.Add(new TextRun(normalized, bold, italic, underline));
                    break;
                case IDomElement el:
                    switch (el.TagName.ToLowerInvariant())
                    {
                        case "br": runs.Add(new TextRun("\n", bold, italic, underline)); break;
                        case "strong" or "b": runs.AddRange(CollectRuns(el, true, italic, underline)); break;
                        case "em" or "i": runs.AddRange(CollectRuns(el, bold, true, underline)); break;
                        case "u": runs.AddRange(CollectRuns(el, bold, italic, true)); break;
                        default: runs.AddRange(CollectRuns(el, bold, italic, underline)); break;
                    }
                    break;
            }
        }
        return runs;
    }

    private static string Normalize(string text) => Regex.Replace(text, @"\s+", " ");

    private static void RenderBlock(ColumnDescriptor column, Block block)
    {
        switch (block)
        {
            case HeadingBlock heading:
                var fontSize = heading.Level switch { 1 => 16f, 2 => 13f, _ => 11.5f };
                column.Item().PaddingTop(4).Text(text =>
                {
                    text.DefaultTextStyle(x => x.FontSize(fontSize).Bold());
                    RenderRuns(text, heading.Runs);
                });
                break;
            case ParagraphBlock paragraph when paragraph.Runs.Count > 0:
                column.Item().Text(text => RenderRuns(text, paragraph.Runs));
                break;
            case ListBlock list:
                var index = 0;
                foreach (var item in list.Items)
                {
                    var marker = list.Ordered ? $"{++index}. " : "•  ";
                    column.Item().PaddingLeft(12).Row(row =>
                    {
                        row.ConstantItem(18).Text(marker);
                        row.RelativeItem().Text(text => RenderRuns(text, item));
                    });
                }
                break;
        }
    }

    private static void RenderRuns(TextDescriptor text, IReadOnlyList<TextRun> runs)
    {
        foreach (var run in runs)
        {
            var span = text.Span(run.Text);
            if (run.Bold) span.Bold();
            if (run.Italic) span.Italic();
            if (run.Underline) span.Underline();
        }
    }
}
