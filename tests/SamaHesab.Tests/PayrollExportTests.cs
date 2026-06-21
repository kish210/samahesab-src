using SamaHesab.Application.HRM;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>PAY-C2-3 — فایل‌های خروجیِ حقوق (بیمه/مالیات/بانک).</summary>
public class PayrollExportTests
{
    private static readonly PayrollPeriod Period = new(1405, 3, WorkshopCode: "12345", EmployerName: "سما");

    private static readonly PayrollExportRow[] Rows =
    {
        new("علی رضایی", "0012345678", "9988776655", "IR120120000000001234567890",
            WorkDays: 30, InsurableBase: 100_000_000, EmployeeInsurance: 7_000_000,
            EmployerInsurance: 23_000_000, Tax: 0, TaxableIncome: 0, NetPay: 93_000_000),
        new("سارا محمدی", "0023456789", "1122334455", "",
            WorkDays: 28, InsurableBase: 200_000_000, EmployeeInsurance: 14_000_000,
            EmployerInsurance: 46_000_000, Tax: 5_000_000, TaxableIncome: 50_000_000, NetPay: 181_000_000),
    };

    [Fact]
    public void InsuranceList_Has_Header_Rows_And_Totals()
    {
        var csv = PayrollExportService.BuildInsuranceList(Period, Rows);
        var lines = csv.TrimEnd().Split('\n');
        Assert.Equal(4, lines.Length);                       // سرستون + ۲ کارمند + جمع
        Assert.Contains("شماره بیمه", lines[0]);
        Assert.Contains("9988776655", lines[1]);             // شمارهٔ بیمهٔ ردیفِ اول
        var total = lines[3];
        Assert.Contains("300000000", total);                 // جمعِ مشمول
        Assert.Contains("21000000", total);                  // جمعِ سهمِ کارمند (۷+۱۴)
        Assert.Contains("69000000", total);                  // جمعِ سهمِ کارفرما (۲۳+۴۶)
    }

    [Fact]
    public void TaxList_Sums_Tax()
    {
        var csv = PayrollExportService.BuildTaxList(Period, Rows);
        var lines = csv.TrimEnd().Split('\n');
        Assert.Equal(4, lines.Length);
        Assert.Contains("5000000", lines[3]);                // جمعِ مالیات
        Assert.Contains("50000000", lines[3]);               // جمعِ درآمدِ مشمول
    }

    [Fact]
    public void BankFile_Skips_Rows_Without_Sheba()
    {
        var csv = PayrollExportService.BuildBankFile(Rows);
        var lines = csv.TrimEnd().Split('\n');
        // سرستون + فقط ۱ ردیف (سارا شبا ندارد) + جمع = ۳
        Assert.Equal(3, lines.Length);
        Assert.Contains("IR120120000000001234567890", lines[1]);
        Assert.DoesNotContain("سارا محمدی", csv);
        Assert.Contains("93000000", lines[2]);               // جمع = فقط خالصِ علی
    }

    [Fact]
    public void Empty_Input_Produces_Header_And_Zero_Total()
    {
        var csv = PayrollExportService.BuildBankFile(System.Array.Empty<PayrollExportRow>());
        var lines = csv.TrimEnd().Split('\n');
        Assert.Equal(2, lines.Length);                       // سرستون + جمعِ صفر
        Assert.Contains("0", lines[1]);
    }
}
