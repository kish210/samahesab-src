using System.Globalization;
using System.Text;

namespace SamaHesab.Application.Reports.Export;

/// <summary>یک جدول گزارش عمومی برای خروجی‌گیری (مستقل از نوع گزارش).</summary>
public record ReportTable(string Title, IReadOnlyList<string> Headers, IReadOnlyList<string[]> Rows)
{
    /// <summary>ساخت از داده‌ی نوع‌دار با انتخاب ستون‌ها.</summary>
    public static ReportTable From<T>(string title, IReadOnlyList<string> headers,
        IEnumerable<T> data, Func<T, object?[]> selector)
    {
        var rows = data.Select(d => selector(d).Select(Cell).ToArray()).ToList();
        return new ReportTable(title, headers, rows);
    }

    private static string Cell(object? v) => v switch
    {
        null => "",
        decimal d => d.ToString("0.##", CultureInfo.InvariantCulture),
        double db => db.ToString("0.##", CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => v.ToString() ?? ""
    };
}

/// <summary>
/// موتور خروجی‌گیری گزارش‌ها — منطق خالص و تست‌پذیر.
/// CSV (باز در اکسل) و HTML (راست‌چین، قابل‌چاپ/تبدیل به PDF). بدون وابستگی خارجی.
/// </summary>
public static class ReportExporter
{
    public static string ToCsv(ReportTable t)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", t.Headers.Select(Escape)));
        foreach (var row in t.Rows)
            sb.AppendLine(string.Join(",", row.Select(Escape)));
        return sb.ToString();
    }

    public static string ToHtml(ReportTable t)
    {
        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html><html dir=\"rtl\" lang=\"fa\"><head><meta charset=\"utf-8\">");
        sb.Append("<style>body{font-family:Tahoma,sans-serif}table{border-collapse:collapse;width:100%}")
          .Append("th,td{border:1px solid #999;padding:4px 8px;text-align:right}th{background:#eee}")
          .Append("h2{text-align:center}</style></head><body>");
        sb.Append("<h2>").Append(Html(t.Title)).Append("</h2><table><thead><tr>");
        foreach (var h in t.Headers) sb.Append("<th>").Append(Html(h)).Append("</th>");
        sb.Append("</tr></thead><tbody>");
        foreach (var row in t.Rows)
        {
            sb.Append("<tr>");
            foreach (var c in row) sb.Append("<td>").Append(Html(c)).Append("</td>");
            sb.Append("</tr>");
        }
        sb.Append("</tbody></table></body></html>");
        return sb.ToString();
    }

    /// <summary>قاعده‌ی CSV (RFC 4180): محصورسازی در صورت وجود ، یا " یا خط جدید.</summary>
    private static string Escape(string field)
    {
        field ??= "";
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
            return "\"" + field.Replace("\"", "\"\"") + "\"";
        return field;
    }

    private static string Html(string s) => (s ?? "")
        .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
