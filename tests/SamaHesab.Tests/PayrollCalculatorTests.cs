using SamaHesab.Application.HRM;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>M7 — موتورِ خالصِ محاسبهٔ حقوق (`PayrollCalculator`).</summary>
public class PayrollCalculatorTests
{
    [Fact]
    public void Below_Exemption_Has_No_Tax()
    {
        var r = PayrollCalculator.Compute(new PayrollInput(8_000_000m), monthlyTaxExemption: 10_000_000m);
        Assert.Equal(8_000_000m, r.Gross);
        Assert.Equal(560_000m, r.Insurance);          // ۷٪
        Assert.Equal(0m, r.Tax);                       // taxable=7.44M < معافیت
        Assert.Equal(7_440_000m, r.Net);
    }

    [Fact]
    public void Above_Exemption_Uses_Progressive_Brackets()
    {
        var r = PayrollCalculator.Compute(new PayrollInput(20_000_000m), monthlyTaxExemption: 10_000_000m);
        Assert.Equal(20_000_000m, r.Gross);
        Assert.Equal(1_400_000m, r.Insurance);         // ۷٪
        // taxable = 18.6M → 10%×(15−10)M + 15%×(18.6−15)M = 500k + 540k
        Assert.Equal(1_040_000m, r.Tax);
        Assert.Equal(17_560_000m, r.Net);              // 20M − 1.4M − 1.04M
    }

    [Fact]
    public void Gross_Includes_Overtime_And_Allowances()
    {
        var r = PayrollCalculator.Compute(new PayrollInput(10_000_000m, Overtime: 2_000_000m, Allowances: 1_000_000m));
        Assert.Equal(13_000_000m, r.Gross);
    }

    [Fact]
    public void Zero_Salary_Is_All_Zero()
    {
        var r = PayrollCalculator.Compute(new PayrollInput(0));
        Assert.Equal(0m, r.Gross);
        Assert.Equal(0m, r.Net);
    }

    [Fact]
    public void Net_Never_Exceeds_Gross()
    {
        var r = PayrollCalculator.Compute(new PayrollInput(50_000_000m));
        Assert.True(r.Net < r.Gross);
        Assert.True(r.Net > 0);
        Assert.Equal(r.Gross - r.Insurance - r.Tax, r.Net);
    }
}
