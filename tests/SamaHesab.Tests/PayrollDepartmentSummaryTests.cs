using System.Linq;
using SamaHesab.Application.HRM;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>خلاصهٔ حقوق به‌تفکیکِ دپارتمان — گروه‌بندی، جمعِ زیرگروه، تعدادِ پرسنل، جمعِ کل.</summary>
public class PayrollDepartmentSummaryTests
{
    private static PayrollDeptRow R(string dep, string name, decimal gross, decimal net, decimal tax, decimal ins)
        => new(dep, name, gross, net, tax, ins);

    [Fact]
    public void Groups_By_Department_With_Subtotals_And_Personnel_Count()
    {
        var r = PayrollDepartmentSummary.Build(new[]
        {
            R("فروش", "علی", 100, 80, 10, 7),
            R("فروش", "رضا", 200, 160, 20, 14),
            R("مالی", "سارا", 300, 250, 30, 21),
        });

        Assert.Equal(2, r.Groups.Count);
        var sales = r.Groups.Single(g => g.Department == "فروش");
        Assert.Equal(2, sales.Count);
        Assert.Equal(300, sales.Gross);
        Assert.Equal(240, sales.Net);
        Assert.Equal(30, sales.Tax);
        Assert.Equal(21, sales.Insurance);

        Assert.Equal(3, r.TotalCount);
        Assert.Equal(600, r.TotalGross);
        Assert.Equal(490, r.TotalNet);
    }

    [Fact]
    public void Empty_Department_Falls_Into_Placeholder_Group()
    {
        var r = PayrollDepartmentSummary.Build(new[]
        {
            R("", "بی‌نام", 100, 90, 5, 7),
            R("   ", "هم‌بی‌نام", 50, 45, 2, 3),
        });

        var g = Assert.Single(r.Groups);
        Assert.Equal(PayrollDepartmentSummary.NoDepartment, g.Department);
        Assert.Equal(2, g.Count);
        Assert.Equal(150, g.Gross);
    }

    [Fact]
    public void ReportTable_Has_SubtotalRow_Per_Group_And_GrandTotal()
    {
        var res = PayrollDepartmentSummary.Build(new[]
        {
            R("فروش", "علی", 100, 80, 10, 7),
            R("مالی", "سارا", 300, 250, 30, 21),
        });
        var t = PayrollDepartmentSummary.ToReportTable(res);

        // ۲ ردیفِ کارمند + ۲ ردیفِ جمعِ گروه + ۱ ردیفِ جمعِ کل = ۵
        Assert.Equal(5, t.Rows.Count);
        Assert.Equal(6, t.Headers.Count);
        Assert.Contains(t.Rows, row => row[0].Contains("جمعِ کل") && row[0].Contains("2 نفر"));
        Assert.Contains(t.Rows, row => row[0].StartsWith("جمعِ فروش"));
    }

    [Fact]
    public void Empty_Input_Yields_Zero_Totals_Without_Crash()
    {
        var r = PayrollDepartmentSummary.Build(System.Array.Empty<PayrollDeptRow>());
        Assert.Empty(r.Groups);
        Assert.Equal(0, r.TotalCount);
        Assert.Equal(0, r.TotalNet);
    }
}
