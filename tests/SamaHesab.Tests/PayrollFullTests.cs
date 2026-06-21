using SamaHesab.Application.HRM;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>PAY-C2-1 — موتورِ کاملِ حقوق (همهٔ اجزای قانونِ کار + نرخ‌های پارامتری).</summary>
public class PayrollFullTests
{
    private static readonly PayrollRates Rates = new(
        InsuranceEmployeeRate: 0.07m, InsuranceEmployerRate: 0.23m,
        MonthlyTaxExemption: 100_000_000m, HoursPerMonth: 220m,
        OvertimeFactor: 1.40m, HolidayFactor: 1.40m, NightShiftFactor: 0.35m,
        ChildAllowancePerChild: 5_000_000m, MaxChildren: 2);

    [Fact]
    public void Overtime_Uses_HourlyRate_Times_Factor()
    {
        // پایه+سنوات = 220,000,000 → نرخِ ساعتی = 1,000,000 ؛ ۱۰ ساعت ×۱.۴ = 14,000,000
        var r = PayrollCalculator.ComputeFull(
            new FullPayrollInput(BaseSalary: 200_000_000, SeniorityBase: 20_000_000, OvertimeHours: 10), Rates);
        Assert.Equal(14_000_000m, r.OvertimePay);
    }

    [Fact]
    public void ChildAllowance_Capped_And_Excluded_From_Insurance()
    {
        var r = PayrollCalculator.ComputeFull(
            new FullPayrollInput(BaseSalary: 100_000_000, Children: 5), Rates);
        Assert.Equal(10_000_000m, r.ChildAllowance);             // سقفِ ۲ فرزند × ۵م
        Assert.Equal(100_000_000m, r.InsurableBase);            // حق اولاد مشمولِ بیمه نیست
        Assert.Equal(110_000_000m, r.Gross);                   // ناخالص شاملِ حق اولاد
        Assert.Equal(7_000_000m, r.EmployeeInsurance);          // ۷٪ روی مشمول (نه ناخالص)
    }

    [Fact]
    public void Employee_And_Employer_Insurance_On_Insurable_Base()
    {
        var r = PayrollCalculator.ComputeFull(
            new FullPayrollInput(BaseSalary: 100_000_000, HousingAllowance: 9_000_000, FoodAllowance: 11_000_000), Rates);
        Assert.Equal(120_000_000m, r.InsurableBase);
        Assert.Equal(8_400_000m, r.EmployeeInsurance);   // ۷٪
        Assert.Equal(27_600_000m, r.EmployerInsurance);  // ۲۳٪
    }

    [Fact]
    public void Net_Subtracts_All_Deductions()
    {
        var r = PayrollCalculator.ComputeFull(
            new FullPayrollInput(BaseSalary: 100_000_000, AdvanceDeduction: 5_000_000, OtherDeductions: 1_000_000), Rates);
        // مشمول=100م، بیمه=7م، مالیات=0 (زیرِ معافیت)، کسورات=7+5+1=13م → خالص=87م
        Assert.Equal(0m, r.Tax);
        Assert.Equal(13_000_000m, r.TotalDeductions);
        Assert.Equal(87_000_000m, r.Net);
    }

    [Fact]
    public void Tax_Kicks_In_Above_Exemption()
    {
        // ناخالص بزرگ تا از معافیتِ ۱۰۰م عبور کند.
        var r = PayrollCalculator.ComputeFull(new FullPayrollInput(BaseSalary: 300_000_000), Rates);
        Assert.True(r.Tax > 0);
        Assert.Equal(300_000_000m, r.Gross);
        // خالص = ناخالص − بیمه − مالیات
        Assert.Equal(r.Gross - r.EmployeeInsurance - r.Tax, r.Net);
    }

    [Fact]
    public void Zero_Salary_Returns_Empty()
    {
        var r = PayrollCalculator.ComputeFull(new FullPayrollInput(BaseSalary: 0), Rates);
        Assert.Equal(0m, r.Gross);
        Assert.Equal(0m, r.Net);
    }

    [Fact]
    public void Base_Compute_Still_Works_For_BackCompat()
    {
        var r = PayrollCalculator.Compute(new PayrollInput(50_000_000, Overtime: 5_000_000), monthlyTaxExemption: 100_000_000);
        Assert.Equal(55_000_000m, r.Gross);
        Assert.Equal(3_850_000m, r.Insurance);   // ۷٪
        Assert.Equal(0m, r.Tax);
    }
}
