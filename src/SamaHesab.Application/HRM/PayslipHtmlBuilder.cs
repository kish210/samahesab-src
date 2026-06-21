using System.Globalization;
using System.Text;

namespace SamaHesab.Application.HRM;

/// <summary>سرستونِ فیشِ حقوقی — اطلاعاتِ کارگاه و کارمند و دوره.</summary>
public record PayslipHeader(
    string CompanyName,        // نامِ کارگاه/شرکت
    string EmployeeName,       // نامِ کارمند
    string PersonnelCode,      // کدِ پرسنلی
    string NationalCode,       // کدِ ملی
    int Year, int Month,       // سال/ماهِ شمسی
    int WorkDays = 30,         // روزهای کارکرد
    string JobTitle = "");     // عنوانِ شغلی

/// <summary>
/// سازندهٔ فیشِ حقوقیِ چاپ — HTMLِ راست‌چینِ A5، منطقِ خالص و تست‌پذیر (بدونِ WPF/DB).
/// از خروجیِ موتورِ <see cref="PayrollCalculator.ComputeFull"/> تغذیه می‌شود.
/// لایهٔ WPF می‌تواند همین HTML را در WebView/چاپ نمایش دهد.
/// </summary>
public static class PayslipHtmlBuilder
{
    private static readonly string[] Months =
    {
        "", "فروردین", "اردیبهشت", "خرداد", "تیر", "مرداد", "شهریور",
        "مهر", "آبان", "آذر", "دی", "بهمن", "اسفند"
    };

    public static string Build(PayslipHeader h, FullPayrollResult r)
    {
        var monthName = h.Month >= 1 && h.Month <= 12 ? Months[h.Month] : h.Month.ToString();
        var sb = new StringBuilder();
        sb.Append("<!DOCTYPE html><html dir=\"rtl\" lang=\"fa\"><head><meta charset=\"utf-8\">");
        sb.Append("<style>")
          .Append("@page{size:A5;margin:10mm}")
          .Append("body{font-family:Tahoma,'Vazirmatn',sans-serif;color:#111;font-size:12px}")
          .Append("h1{text-align:center;font-size:16px;margin:0 0 2px}")
          .Append(".sub{text-align:center;color:#555;margin:0 0 8px}")
          .Append("table{border-collapse:collapse;width:100%;margin:6px 0}")
          .Append("td,th{border:1px solid #aaa;padding:3px 6px}")
          .Append("th{background:#1E3A5F;color:#fff;text-align:center}")
          .Append(".amt{text-align:left;direction:ltr;font-variant-numeric:tabular-nums}")
          .Append(".tot{font-weight:bold;background:#EEF2F7}")
          .Append(".net{font-weight:bold;font-size:14px;background:#E8F0E8}")
          .Append(".two{display:flex;gap:8px}.two>div{flex:1}")
          .Append("</style></head><body>");

        sb.Append("<h1>").Append(Html(h.CompanyName)).Append("</h1>");
        sb.Append("<p class=\"sub\">فیش حقوقی — ").Append(Html(monthName)).Append(' ').Append(Fa(h.Year)).Append("</p>");

        // اطلاعاتِ کارمند
        sb.Append("<table>")
          .Append(Tr2("نام و نام خانوادگی", h.EmployeeName, "کد پرسنلی", h.PersonnelCode))
          .Append(Tr2("کد ملی", h.NationalCode, "عنوان شغلی", h.JobTitle))
          .Append(Tr2("روزهای کارکرد", Fa(h.WorkDays), "دوره", $"{monthName} {Fa(h.Year)}"))
          .Append("</table>");

        // ستونِ دوتایی: مزایا | کسورات
        sb.Append("<div class=\"two\">");

        // مزایا
        sb.Append("<div><table><tr><th colspan=\"2\">مزایا و پرداختی‌ها</th></tr>");
        sb.Append(RowIf("اضافه‌کاری", r.OvertimePay));
        sb.Append(RowIf("شب‌کاری", r.NightPay));
        sb.Append(RowIf("جمعه/تعطیل‌کاری", r.HolidayPay));
        sb.Append(RowIf("حق اولاد", r.ChildAllowance));
        sb.Append("<tr class=\"tot\"><td>جمع ناخالص</td><td class=\"amt\">").Append(Money(r.Gross)).Append("</td></tr>");
        sb.Append("</table></div>");

        // کسورات
        sb.Append("<div><table><tr><th colspan=\"2\">کسورات</th></tr>");
        sb.Append(Row("بیمهٔ سهم کارمند (۷٪)", r.EmployeeInsurance));
        sb.Append(RowIf("مالیات حقوق", r.Tax));
        sb.Append("<tr class=\"tot\"><td>جمع کسورات</td><td class=\"amt\">").Append(Money(r.TotalDeductions)).Append("</td></tr>");
        sb.Append("</table></div>");

        sb.Append("</div>"); // .two

        // خالص + سهمِ کارفرما (اطلاعاتی)
        sb.Append("<table>")
          .Append("<tr><td>مأخذ مشمول بیمه</td><td class=\"amt\">").Append(Money(r.InsurableBase)).Append("</td>")
          .Append("<td>بیمهٔ سهم کارفرما (۲۳٪)</td><td class=\"amt\">").Append(Money(r.EmployerInsurance)).Append("</td></tr>")
          .Append("<tr class=\"net\"><td>خالص پرداختی</td><td class=\"amt\" colspan=\"3\">")
          .Append(Money(r.Net)).Append(" ریال</td></tr>")
          .Append("</table>");

        sb.Append("<p class=\"sub\" style=\"margin-top:10px\">این فیش به‌صورتِ سیستمی صادر شده و نیازی به مهر و امضا ندارد.</p>");
        sb.Append("</body></html>");
        return sb.ToString();
    }

    private static string Row(string label, decimal v) =>
        $"<tr><td>{Html(label)}</td><td class=\"amt\">{Money(v)}</td></tr>";

    private static string RowIf(string label, decimal v) => v != 0 ? Row(label, v) : "";

    private static string Tr2(string k1, string v1, string k2, string v2) =>
        $"<tr><td>{Html(k1)}</td><td>{Html(v1)}</td><td>{Html(k2)}</td><td>{Html(v2)}</td></tr>";

    private static string Money(decimal v) =>
        Fa(Math.Round(v, 0, MidpointRounding.AwayFromZero).ToString("#,##0", CultureInfo.InvariantCulture));

    /// <summary>ارقامِ لاتین → فارسی.</summary>
    private static string Fa(object o)
    {
        var s = o?.ToString() ?? "";
        var sb = new StringBuilder(s.Length);
        foreach (var c in s) sb.Append(c >= '0' && c <= '9' ? (char)('۰' + (c - '0')) : c);
        return sb.ToString();
    }

    private static string Html(string s) => (s ?? "")
        .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}
