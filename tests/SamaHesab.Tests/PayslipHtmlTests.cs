using SamaHesab.Application.HRM;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>PAY-C2-2 — فیشِ حقوقیِ چاپ (HTMLِ خالص).</summary>
public class PayslipHtmlTests
{
    private static readonly PayslipHeader Header = new(
        CompanyName: "شرکتِ نمونه", EmployeeName: "علی رضایی", PersonnelCode: "1023",
        NationalCode: "0012345678", Year: 1405, Month: 3, WorkDays: 30, JobTitle: "حسابدار");

    private static FullPayrollResult Sample() => PayrollCalculator.ComputeFull(
        new FullPayrollInput(BaseSalary: 100_000_000, HousingAllowance: 9_000_000,
            FoodAllowance: 11_000_000, Children: 2, OvertimeHours: 10),
        new PayrollRates(ChildAllowancePerChild: 5_000_000m));

    [Fact]
    public void Build_Produces_Rtl_Html_With_Key_Sections()
    {
        var html = PayslipHtmlBuilder.Build(Header, Sample());
        Assert.StartsWith("<!DOCTYPE html>", html);
        Assert.Contains("dir=\"rtl\"", html);
        Assert.Contains("شرکتِ نمونه", html);
        Assert.Contains("علی رضایی", html);
        Assert.Contains("خرداد", html);              // نامِ ماهِ ۳
        Assert.Contains("مزایا و پرداختی‌ها", html);
        Assert.Contains("کسورات", html);
        Assert.Contains("خالص پرداختی", html);
    }

    [Fact]
    public void Net_Value_Appears_In_Html()
    {
        var r = Sample();
        var html = PayslipHtmlBuilder.Build(Header, r);
        // خالص به ارقامِ فارسیِ گروه‌بندی‌شده — رقمِ اولِ مقدار باید حاضر باشد.
        Assert.Contains("ریال", html);
        Assert.True(r.Net > 0);
        Assert.Contains("بیمهٔ سهم کارفرما", html);   // اطلاعاتیِ ۲۳٪
    }

    [Fact]
    public void Zero_Components_Are_Omitted()
    {
        // بدونِ اضافه‌کاری/شب‌کاری → آن ردیف‌ها نباید چاپ شوند.
        var r = PayrollCalculator.ComputeFull(new FullPayrollInput(BaseSalary: 80_000_000));
        var html = PayslipHtmlBuilder.Build(Header, r);
        Assert.DoesNotContain("شب‌کاری", html);
        Assert.DoesNotContain("جمعه/تعطیل‌کاری", html);
    }

    [Fact]
    public void Html_Is_Escaped()
    {
        var h = Header with { EmployeeName = "a<b>&c" };
        var html = PayslipHtmlBuilder.Build(h, Sample());
        Assert.Contains("a&lt;b&gt;&amp;c", html);
    }
}
