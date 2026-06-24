using System.Globalization;
using SamaHesab.Application.Reports.Export;

namespace SamaHesab.Application.HRM;

/// <summary>یک ردیفِ ورودیِ خلاصهٔ دپارتمانی (دپارتمان + کارمند + مبالغِ فیش).</summary>
public record PayrollDeptRow(string Department, string EmployeeName,
    decimal Gross, decimal NetPay, decimal Tax, decimal EmployeeInsurance);

/// <summary>گروهِ یک دپارتمان: ردیف‌ها + تعدادِ پرسنل + جمعِ زیرگروه.</summary>
public record PayrollDeptGroup(string Department, IReadOnlyList<PayrollDeptRow> Rows,
    int Count, decimal Gross, decimal Net, decimal Tax, decimal Insurance);

public record PayrollDeptSummaryResult(IReadOnlyList<PayrollDeptGroup> Groups,
    int TotalCount, decimal TotalGross, decimal TotalNet, decimal TotalTax, decimal TotalInsurance);

/// <summary>
/// خلاصهٔ حقوق به‌تفکیکِ دپارتمان — منطقِ خالص و تست‌پذیر. رودمپ-حقوق: «ستونِ دپارتمان خالی است؛
/// گروه‌بندی و جمعِ زیرگروه + تعدادِ پرسنل». فیش‌های یک دوره را بر دپارتمان گروه‌بندی و جمع می‌بندد.
/// </summary>
public static class PayrollDepartmentSummary
{
    public const string NoDepartment = "بدون دپارتمان";

    public static PayrollDeptSummaryResult Build(IEnumerable<PayrollDeptRow> rows)
    {
        var groups = rows
            .GroupBy(r => string.IsNullOrWhiteSpace(r.Department) ? NoDepartment : r.Department.Trim())
            .OrderBy(g => g.Key, StringComparer.Create(new CultureInfo("fa-IR"), ignoreCase: true))
            .Select(g =>
            {
                var list = g.OrderBy(r => r.EmployeeName, StringComparer.Create(new CultureInfo("fa-IR"), false)).ToList();
                return new PayrollDeptGroup(
                    g.Key, list, list.Count,
                    list.Sum(r => r.Gross), list.Sum(r => r.NetPay),
                    list.Sum(r => r.Tax), list.Sum(r => r.EmployeeInsurance));
            })
            .ToList();

        return new PayrollDeptSummaryResult(
            groups,
            groups.Sum(g => g.Count),
            groups.Sum(g => g.Gross), groups.Sum(g => g.Net),
            groups.Sum(g => g.Tax), groups.Sum(g => g.Insurance));
    }

    /// <summary>تخت‌سازی به ReportTable برای خروجیِ CSV/HTML — هر گروه + ردیفِ «جمعِ دپارتمان»، و ردیفِ پایانیِ «جمعِ کل».</summary>
    public static ReportTable ToReportTable(PayrollDeptSummaryResult r, string title = "خلاصهٔ حقوق به‌تفکیکِ دپارتمان")
    {
        var headers = new[] { "دپارتمان", "کارمند", "ناخالص", "بیمهٔ کارمند", "مالیات", "خالص پرداختی" };
        var rows = new List<string[]>();
        foreach (var g in r.Groups)
        {
            foreach (var e in g.Rows)
                rows.Add(new[] { g.Department, e.EmployeeName, M(e.Gross), M(e.EmployeeInsurance), M(e.Tax), M(e.NetPay) });
            rows.Add(new[] { $"جمعِ {g.Department} ({g.Count} نفر)", "", M(g.Gross), M(g.Insurance), M(g.Tax), M(g.Net) });
        }
        rows.Add(new[] { $"جمعِ کل ({r.TotalCount} نفر)", "", M(r.TotalGross), M(r.TotalInsurance), M(r.TotalTax), M(r.TotalNet) });
        return new ReportTable(title, headers, rows);
    }

    private static string M(decimal v) =>
        Math.Round(v, 0, MidpointRounding.AwayFromZero).ToString("#,0", CultureInfo.InvariantCulture);
}
