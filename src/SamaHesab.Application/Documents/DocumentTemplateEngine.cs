using System.Linq;
using System.Text.RegularExpressions;
using SamaHesab.Domain.Entities.Documents;

namespace SamaHesab.Application.Documents;

/// <summary>
/// فاز ۱۰ — دادهٔ یک سند برای رندرِ قالب: مجموعهٔ متغیرهای اسکالر (سرسند) + ردیف‌های اقلام.
/// کلیدها حساس به بزرگی/کوچکی نیستند.
/// </summary>
public sealed record DocumentData(
    IReadOnlyDictionary<string, string?> Fields,
    IReadOnlyList<IReadOnlyDictionary<string, string?>> Rows)
{
    public static DocumentData Of(IReadOnlyDictionary<string, string?> fields,
        IReadOnlyList<IReadOnlyDictionary<string, string?>>? rows = null)
        => new(new Dictionary<string, string?>(fields, StringComparer.OrdinalIgnoreCase),
               rows ?? System.Array.Empty<IReadOnlyDictionary<string, string?>>());
}

/// <summary>
/// موتورِ خالصِ قالب: قالب (HTML با توکن) + داده → HTML نهایی. بدونِ وابستگی به UI/EF.
/// نحوِ توکن:
///   • اسکالر: <c>{InvoiceNumber}</c> ، <c>{CustomerName}</c> … از <see cref="DocumentData.Fields"/>.
///   • بلوکِ ردیف: محتوای میانِ <c>[[ROWS]]</c> و <c>[[/ROWS]]</c> برای هر ردیف تکرار می‌شود؛
///     داخلِ آن <c>{ColumnName}</c> از همان ردیف و <c>{#}</c> = شمارهٔ ردیف.
///   • توکنِ ناشناخته → رشتهٔ خالی (قالب نشت نمی‌کند).
/// </summary>
public static class DocumentTemplateEngine
{
    private static readonly Regex TokenRx = new(@"\{(#|[A-Za-z0-9_]+)\}", RegexOptions.Compiled);
    private static readonly Regex RowsRx =
        new(@"\[\[ROWS\]\](.*?)\[\[/ROWS\]\]", RegexOptions.Compiled | RegexOptions.Singleline);

    /// <summary>رندرِ یک قطعهٔ قالب با داده (اول بلوکِ ردیف‌ها، سپس توکن‌های اسکالر).</summary>
    public static string Render(string? templateHtml, DocumentData data)
    {
        if (string.IsNullOrEmpty(templateHtml)) return string.Empty;
        var expanded = ExpandRows(templateHtml!, data);
        return ResolveScalars(expanded, data.Fields);
    }

    /// <summary>رندرِ کاملِ یک قالب (Header + Body + Footer) با داده.</summary>
    public static string RenderTemplate(DocumentTemplate template, DocumentData data)
        => Render(template.HeaderHtml, data) + Render(template.BodyHtml, data) + Render(template.FooterHtml, data);

    private static string ExpandRows(string html, DocumentData data)
        => RowsRx.Replace(html, m =>
        {
            var inner = m.Groups[1].Value;
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < data.Rows.Count; i++)
            {
                var row = data.Rows[i];
                sb.Append(TokenRx.Replace(inner, tm =>
                {
                    var key = tm.Groups[1].Value;
                    if (key == "#") return Fa((i + 1).ToString());
                    if (row.TryGetValue(key, out var v) && v is not null) return v;
                    if (data.Fields.TryGetValue(key, out var fv) && fv is not null) return fv;
                    return string.Empty;
                }));
            }
            return sb.ToString();
        });

    /// <summary>ارقامِ لاتین → فارسی (فقط برای شمارهٔ ردیفِ {#}).</summary>
    private static string Fa(string s)
    {
        var sb = new System.Text.StringBuilder(s.Length);
        foreach (var c in s) sb.Append(c >= '0' && c <= '9' ? (char)('۰' + (c - '0')) : c);
        return sb.ToString();
    }

    private static string ResolveScalars(string html, IReadOnlyDictionary<string, string?> fields)
        => TokenRx.Replace(html, m =>
        {
            var key = m.Groups[1].Value;
            return fields.TryGetValue(key, out var v) && v is not null ? v : string.Empty;
        });

    /// <summary>فهرستِ توکن‌های یکتایِ به‌کاررفته در یک قالب (برای راهنمایِ طراح).</summary>
    public static IReadOnlyList<string> ExtractTokens(string? html)
    {
        if (string.IsNullOrEmpty(html)) return System.Array.Empty<string>();
        return TokenRx.Matches(html!).Select(m => m.Groups[1].Value)
            .Where(t => t != "#").Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }
}
