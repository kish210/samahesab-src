using System.Linq;
using SamaHesab.Application.Contracting;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>CON-C2-3 — گزارش‌سازِ پیمانکاری (صورت‌وضعیت/خلاصهٔ پروژه/سپرده/ضمانت‌نامه).</summary>
public class ContractingReportBuilderTests
{
    [Fact]
    public void StatementPrintout_Lists_Waterfall_Lines()
    {
        var t = ContractingReportBuilder.StatementPrintout(new StatementPrintoutData(
            "ساختمان", "P1", "شهرداری", 1, "موقت", "1404/02/01",
            CumulativeGrossWork: 400_000, PreviousCumulative: 0, PeriodWork: 400_000,
            AdjustmentAmount: 0, MaterialDiffAmount: 0, GrossThisPeriod: 400_000,
            AdvanceRecovery: 100_000, Retention: 20_000, Insurance: 20_000, Tax: 20_000, Penalty: 0, Other: 0,
            NetPayable: 240_000));

        Assert.Contains("صورت‌وضعیتِ موقت", t.Title);
        Assert.Contains(t.Rows, r => r[0] == "خالصِ قابلِ پرداخت" && r[1] == "240,000");
        Assert.Contains(t.Rows, r => r[0] == "کسر: حسن‌انجام‌کار" && r[1] == "20,000");
    }

    [Fact]
    public void ProjectFinancialSummary_Adds_Total_Row_And_Progress()
    {
        var t = ContractingReportBuilder.ProjectFinancialSummary(new[]
        {
            new ProjectSummaryRow("P1", "ساختمان", 1_000_000, 700_000, 35_000, 35_000, 100_000, 595_000),
            new ProjectSummaryRow("P2", "محوطه", 500_000, 250_000, 12_500, 12_500, 0, 225_000),
        });

        // ردیفِ جمع: مبلغِ پیمان ۱٫۵م، کارکرد ۹۵۰هزار.
        var total = t.Rows.Last();
        Assert.Equal("جمع", total[1]);
        Assert.Equal("1,500,000", total[2]);
        Assert.Equal("950,000", total[3]);
        // پیشرفتِ P1 = ۷۰٪
        Assert.Contains(t.Rows, r => r[0] == "P1" && r[4] == "70٪");
    }

    [Fact]
    public void DepositsHeld_Sums_Retention_And_Insurance()
    {
        var t = ContractingReportBuilder.DepositsHeld(new[]
        {
            new DepositRow("ساختمان", 35_000, 35_000),
            new DepositRow("محوطه", 12_500, 12_500),
        });
        var total = t.Rows.Last();
        Assert.Equal("جمع", total[0]);
        Assert.Equal("47,500", total[1]);   // حسن‌انجام
        Assert.Equal("95,000", total[3]);   // جمعِ کل
    }

    [Fact]
    public void GuaranteeRegister_Shows_DaysToExpiry()
    {
        var t = ContractingReportBuilder.GuaranteeRegister(new[]
        {
            new GuaranteeRow("ساختمان", "حسن‌انجام‌کار", "ملت", 50_000, "1404/12/29", 200, "فعال"),
            new GuaranteeRow("محوطه", "پیش‌پرداخت", "ملی", 30_000, "1404/06/15", null, "آزادشده"),
        });
        Assert.Equal(2, t.Rows.Count);
        Assert.Contains(t.Rows, r => r[0] == "ساختمان" && r[5] == "200");
        Assert.Contains(t.Rows, r => r[0] == "محوطه" && r[5] == "—");
    }
}
