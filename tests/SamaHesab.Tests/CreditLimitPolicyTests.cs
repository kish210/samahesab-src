using SamaHesab.Application.CRM;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>کار #۳۷ — سیاست سقف اعتبار مشتری (منطق خالص).</summary>
public class CreditLimitPolicyTests
{
    [Fact]
    public void Zero_Limit_Means_Unlimited()
        => Assert.False(CreditLimitPolicy.IsBlocked(9_000_000, 5_000_000, 0));

    [Fact]
    public void Within_Limit_Is_Allowed()
        => Assert.False(CreditLimitPolicy.IsBlocked(2_000_000, 1_000_000, 5_000_000));

    [Fact]
    public void Exceeding_Limit_Is_Blocked()
        => Assert.True(CreditLimitPolicy.IsBlocked(4_000_000, 2_000_000, 5_000_000));

    [Fact]
    public void Cash_Only_Sale_Is_Never_Blocked()
        => Assert.False(CreditLimitPolicy.IsBlocked(9_000_000, 0, 5_000_000));

    [Theory]
    [InlineData(2_000_000, 5_000_000, 3_000_000)]   // باقی‌مانده = سقف - مانده
    public void Available_Computes_Remaining(decimal balance, decimal limit, decimal expected)
        => Assert.Equal(expected, CreditLimitPolicy.Available(balance, limit));

    [Fact]
    public void Available_Is_Max_When_Unlimited()
        => Assert.Equal(decimal.MaxValue, CreditLimitPolicy.Available(1_000_000, 0));
}
