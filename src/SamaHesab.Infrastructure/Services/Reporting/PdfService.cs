using System.Text;
using System.Text.RegularExpressions;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Reports.Export;

namespace SamaHesab.Infrastructure.Services.Reporting;

/// <summary>
/// فاز ۱۱ — P2/DT-7: تولیدِ PDFِ بومیِ فارسی با QuestPDF.
/// QuestPDF برخلافِ PdfSharpCore شکل‌دهی/اتصالِ حروفِ فارسی و راست‌چین را درست انجام می‌دهد
/// (موتورِ متنیِ مبتنی بر Skia). از قلمِ Tahoma که روی همهٔ ویندوزها هست استفاده می‌کنیم.
/// </summary>
public sealed class PdfService : IPdfService
{
    private const string Font = "Tahoma";

    static PdfService()
    {
        // مجوزِ نسخهٔ Community (رایگان تا سقفِ درآمدِ مجاز) — لازم برای تولیدِ بدونِ واترمارک.
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] RenderTable(ReportTable table, PdfMeta? meta = null)
    {
        meta ??= new PdfMeta();
        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                ConfigurePage(page, meta, table.Title);
                page.Content().PaddingVertical(8).Table(t =>
                {
                    t.ColumnsDefinition(cols =>
                    {
                        for (int i = 0; i < table.Headers.Count; i++) cols.RelativeColumn();
                    });

                    t.Header(h =>
                    {
                        foreach (var head in table.Headers)
                            h.Cell().Element(HeaderCell).Text(head);
                    });

                    bool stripe = false;
                    foreach (var row in table.Rows)
                    {
                        var bg = stripe ? Colors.Grey.Lighten4 : Colors.White;
                        stripe = !stripe;
                        foreach (var cell in row)
                            t.Cell().Background(bg).Element(BodyCell).Text(cell ?? "");
                    }
                });
            });
        }).GeneratePdf();
    }

    public byte[] RenderHtmlDocument(string html, PdfMeta? meta = null)
    {
        meta ??= new PdfMeta();
        var parts = SplitHtmlParts(html);
        return Document.Create(doc =>
        {
            doc.Page(page =>
            {
                ConfigurePage(page, meta, meta.Subtitle ?? "سند");
                page.Content().PaddingVertical(8).Column(col =>
                {
                    col.Spacing(3);
                    foreach (var part in parts)
                    {
                        if (part.Image is { } bytes)
                            // تصویرِ embedـشده (مثلِ QR/بارکد) — راست‌چین، با عرضِ مشخص‌شده در HTML.
                            col.Item().AlignRight().Width(part.WidthPx).Image(bytes).FitWidth();
                        else
                            foreach (var line in HtmlToLines(part.Html!))
                                col.Item().Text(line).FontSize(10);
                    }
                });
            });
        }).GeneratePdf();
    }

    // ---- چیدمانِ مشترکِ صفحه ----

    private static void ConfigurePage(PageDescriptor page, PdfMeta meta, string title)
    {
        page.Size(meta.Landscape ? PageSizes.A4.Landscape() : PageSizes.A4);
        page.Margin(25);
        page.ContentFromRightToLeft();
        page.DefaultTextStyle(x => x.FontFamily(Font).FontSize(9).DirectionFromRightToLeft());

        page.Header().Column(col =>
        {
            if (!string.IsNullOrWhiteSpace(meta.CompanyName))
                col.Item().Text(meta.CompanyName).FontSize(13).Bold().FontColor(Colors.Blue.Darken3);
            col.Item().Text(title).FontSize(12).SemiBold();
            if (!string.IsNullOrWhiteSpace(meta.Subtitle))
                col.Item().Text(meta.Subtitle!).FontSize(9).FontColor(Colors.Grey.Darken1);
            col.Item().PaddingTop(4).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
        });

        page.Footer().Row(row =>
        {
            var stamp = meta.GeneratedAt ?? DateTime.Now.ToString("yyyy/MM/dd HH:mm");
            row.RelativeItem().Text($"تاریخِ تولید: {stamp} — سماحساب").FontSize(8).FontColor(Colors.Grey.Medium);
            row.AutoItem().Text(t =>
            {
                t.DefaultTextStyle(s => s.FontSize(8).FontColor(Colors.Grey.Medium));
                t.Span("صفحهٔ ");
                t.CurrentPageNumber();
                t.Span(" از ");
                t.TotalPages();
            });
        });
    }

    private static IContainer HeaderCell(IContainer c) => c
        .Background(Colors.Blue.Darken3)
        .Border(0.5f).BorderColor(Colors.Grey.Lighten1)
        .Padding(5)
        .DefaultTextStyle(x => x.FontColor(Colors.White).Bold().FontSize(9));

    private static IContainer BodyCell(IContainer c) => c
        .Border(0.5f).BorderColor(Colors.Grey.Lighten2)
        .Padding(4)
        .DefaultTextStyle(x => x.FontSize(9));

    // ---- تفکیکِ HTML به قطعاتِ متن/تصویر (برای embedِ QR/بارکد در PDF) ----

    /// <summary>یک قطعه از سند: یا متنِ HTML یا یک تصویرِ embedـشده (data:base64) با عرضِ موردنظر.</summary>
    private sealed record HtmlPart(string? Html, byte[]? Image, float WidthPx);

    private static readonly Regex DataImgRx = new(
        "<img[^>]*?src=\"data:image/(?:png|jpe?g);base64,(?<b64>[^\"]+)\"[^>]*?>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex WidthAttrRx = new("width=\"(?<w>\\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>HTML را به‌ترتیب به قطعاتِ متن و تصویرِ data-URI می‌شکند تا تصاویر در PDF واقعی رندر شوند.</summary>
    private static List<HtmlPart> SplitHtmlParts(string? html)
    {
        var parts = new List<HtmlPart>();
        if (string.IsNullOrWhiteSpace(html)) return parts;

        int pos = 0;
        foreach (Match m in DataImgRx.Matches(html))
        {
            if (m.Index > pos)
                parts.Add(new HtmlPart(html.Substring(pos, m.Index - pos), null, 0));

            byte[]? bytes = null;
            try { bytes = Convert.FromBase64String(m.Groups["b64"].Value); } catch { /* base64 خراب → نادیده */ }
            if (bytes is { Length: > 0 })
            {
                var wm = WidthAttrRx.Match(m.Value);
                float w = wm.Success && float.TryParse(wm.Groups["w"].Value, out var pw) ? pw : 120f;
                parts.Add(new HtmlPart(null, bytes, w));
            }
            pos = m.Index + m.Length;
        }
        if (pos < html.Length)
            parts.Add(new HtmlPart(html.Substring(pos), null, 0));
        return parts;
    }

    // ---- HTML → خطوطِ متنی (best-effort برای رندرِ سندِ قالب‌محور) ----

    private static List<string> HtmlToLines(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return new();
        var s = Regex.Replace(html, "(?is)<(script|style).*?</\\1>", "");
        s = Regex.Replace(s, "(?i)<(br|/p|/div|/tr|/h[1-6]|/li)\\s*>", "\n");
        s = Regex.Replace(s, "(?i)</td>", "  ");
        s = Regex.Replace(s, "<[^>]+>", "");
        s = System.Net.WebUtility.HtmlDecode(s);
        return s.Split('\n')
                .Select(l => Regex.Replace(l, "\\s+", " ").Trim())
                .Where(l => l.Length > 0)
                .ToList();
    }
}
