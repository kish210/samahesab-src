using SamaHesab.Application.Accounting;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>توازن خودکار ردیف آخر سند — برد اصلی سرعتِ ورود سند حسابداری.</summary>
public class VoucherBalanceTests
{
    [Theory]
    [InlineData(1000, 0, 0, 1000)]     // فقط بدهکار → ردیف بستانکار ۱۰۰۰
    [InlineData(0, 1000, 1000, 0)]     // فقط بستانکار → ردیف بدهکار ۱۰۰۰
    [InlineData(1500, 600, 0, 900)]    // بدهکار بیشتر → بستانکارِ مابه‌التفاوت
    [InlineData(600, 1500, 900, 0)]    // بستانکار بیشتر → بدهکارِ مابه‌التفاوت
    public void BalancingEntry_Fills_Opposite_Side(decimal td, decimal tc, decimal expDebit, decimal expCredit)
    {
        var (debit, credit) = VoucherBalance.BalancingEntry(td, tc);
        Assert.Equal(expDebit, debit);
        Assert.Equal(expCredit, credit);
    }

    [Fact]
    public void BalancingEntry_Returns_Zero_When_Already_Balanced()
    {
        var (debit, credit) = VoucherBalance.BalancingEntry(1000, 1000);
        Assert.Equal(0, debit);
        Assert.Equal(0, credit);
    }

    [Theory]
    [InlineData(1000, 1000, true)]
    [InlineData(1000, 1000.005, true)]  // داخل خطای گرد کردن
    [InlineData(1000, 999, false)]
    public void IsBalanced_Respects_Rounding_Tolerance(decimal td, decimal tc, bool expected)
        => Assert.Equal(expected, VoucherBalance.IsBalanced(td, tc));
}
