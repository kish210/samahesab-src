using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Domain.Enums;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>U-FIXED-ASSET — تستِ واحدِ موتورِ محاسبهٔ استهلاک (بدونِ DB).</summary>
public class FixedAssetDepreciationTests
{
    [Fact]
    public void StraightLine_Monthly_Is_Cost_Minus_Salvage_Divided_By_Life()
    {
        // بهای ۱٬۲۰۰٬۰۰۰، اسقاط ۰، عمر ۱۲ ماه → ماهانه ۱۰۰٬۰۰۰
        Assert.Equal(100000m, DepreciationCalculator.MonthlyStraightLine(1200000m, 0m, 12), 2);
    }

    [Fact]
    public void StraightLine_MultiMonth_Sums_And_Caps_At_Remaining_Depreciable()
    {
        // بهای ۱٬۲۰۰٬۰۰۰، اسقاط ۲۰۰٬۰۰۰، عمر ۱۰ ماه → ماهانه ۱۰۰٬۰۰۰ و مجموعِ قابلِ‌استهلاک = ۱٬۰۰۰٬۰۰۰.
        // درخواستِ ۱۲ ماه (بیشتر از عمر) باید فقط تا ۱٬۰۰۰٬۰۰۰ محاسبه شود، نه ۱٬۲۰۰٬۰۰۰.
        var total = DepreciationCalculator.DepreciationForMonths(
            1200000m, 200000m, 10, DepreciationMethod.StraightLine, 0m, 12);
        Assert.Equal(1000000m, total, 2);
    }

    [Fact]
    public void DecliningBalance_Reduces_Month_Over_Month()
    {
        // بهای ۱٬۰۰۰٬۰۰۰، اسقاط ۰، عمر ۱۲ ماه، نرخ = 2/12.
        var first = DepreciationCalculator.MonthlyDecliningBalance(1000000m, 0m, 12, 1000000m);
        var second = DepreciationCalculator.MonthlyDecliningBalance(1000000m, 0m, 12, 1000000m - first);
        Assert.True(first > second, "استهلاکِ نزولی باید ماه‌به‌ماه کاهش یابد.");
    }

    [Fact]
    public void DecliningBalance_Never_Depreciates_Below_Salvage()
    {
        // بهای ۱٬۰۰۰٬۰۰۰، اسقاط ۱۰۰٬۰۰۰، عمر ۱۲ ماه → در اجرایِ طولانی نباید انباشته از ۹۰۰٬۰۰۰ بیشتر شود.
        var total = DepreciationCalculator.DepreciationForMonths(
            1000000m, 100000m, 12, DepreciationMethod.DecliningBalance, 0m, 60);
        Assert.Equal(900000m, total, 2);
    }

    [Fact]
    public void TotalMonths_Computes_Shamsi_YearMonth()
    {
        // ۱۴۰۴/۱۲ → ماهِ ۱۲ِ سالِ ۱۴۰۴؛ ۱۴۰۵/۰۱ دقیقاً یک ماه بعد است.
        var a = DepreciationCalculator.TotalMonths("1404/12/29");
        var b = DepreciationCalculator.TotalMonths("1405/01/01");
        Assert.Equal(1, b - a);
    }

    [Fact]
    public void Zero_Or_Negative_Months_Produces_No_Depreciation()
    {
        Assert.Equal(0m, DepreciationCalculator.DepreciationForMonths(
            1000000m, 0m, 12, DepreciationMethod.StraightLine, 0m, 0));
        Assert.Equal(0m, DepreciationCalculator.DepreciationForMonths(
            1000000m, 0m, 12, DepreciationMethod.StraightLine, 0m, -3));
    }
}
