using SamaHesab.Application.CRM;
using SamaHesab.Modules.CRM.Domain;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>کار #۳۸ — باشگاه مشتریان (منطق خالص + دامنه).</summary>
public class LoyaltyTests
{
    [Theory]
    [InlineData(250_000, 2)]     // 250k / 100k = 2
    [InlineData(99_000, 0)]      // کمتر از یک امتیاز
    [InlineData(1_000_000, 10)]
    public void EarnedPoints_FloorsByRate(decimal amount, int expected)
        => Assert.Equal(expected, LoyaltyPolicy.EarnedPoints(amount));

    [Theory]
    [InlineData(10, 5, true)]
    [InlineData(10, 10, true)]
    [InlineData(10, 11, false)]
    [InlineData(10, 0, false)]
    public void CanRedeem_Checks_Balance(int balance, int points, bool expected)
        => Assert.Equal(expected, LoyaltyPolicy.CanRedeem(balance, points));

    [Fact]
    public void Redeem_Stores_Negative_Points()
    {
        var earn = LoyaltyTransaction.Earn(5, 100, "خرید");
        var redeem = LoyaltyTransaction.Redeem(5, 30, "استفاده");
        Assert.Equal(100, earn.Points);
        Assert.Equal(-30, redeem.Points);
        Assert.Equal(70, earn.Points + redeem.Points);   // موجودی
        Assert.Equal("کسب", earn.Type);
        Assert.Equal("استفاده", redeem.Type);
    }

    [Fact]
    public void Earn_Rejects_NonPositive()
        => Assert.Throws<ArgumentException>(() => LoyaltyTransaction.Earn(5, 0, "x"));
}
