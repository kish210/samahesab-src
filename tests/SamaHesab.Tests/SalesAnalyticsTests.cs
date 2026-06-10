using SamaHesab.Application.BI;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>موتور هوش تجاری فروش — رتبه‌بندی، روند، سود.</summary>
public class SalesAnalyticsTests
{
    private static readonly SalesRecord[] Sales =
    {
        new("1404/01/05", PartyId: 1, Amount: 100),
        new("1404/01/20", PartyId: 2, Amount: 300),
        new("1404/02/03", PartyId: 1, Amount: 250),
        new("1404/02/15", PartyId: 1, Amount:  50),
    };

    [Fact]
    public void TopParties_Ranks_By_Total_Desc()
    {
        var top = SalesAnalytics.TopParties(Sales, take: 10);
        Assert.Equal(2, top.Count);
        Assert.Equal(1, top[0].Id);          // 100+250+50 = 400
        Assert.Equal(400, top[0].Total);
        Assert.Equal(3, top[0].Count);
        Assert.Equal(2, top[1].Id);          // 300
    }

    [Fact]
    public void TopParties_Respects_Take()
        => Assert.Single(SalesAnalytics.TopParties(Sales, take: 1));

    [Fact]
    public void MonthlyTrend_Groups_By_Persian_Month()
    {
        var trend = SalesAnalytics.MonthlyTrend(Sales);
        Assert.Equal(2, trend.Count);
        Assert.Equal("1404/01", trend[0].Period);
        Assert.Equal(400, trend[0].Total);   // 100+300
        Assert.Equal("1404/02", trend[1].Period);
        Assert.Equal(300, trend[1].Total);   // 250+50
    }

    [Fact]
    public void TopProducts_Ranks_By_Net_And_Sums_Profit()
    {
        var items = new[]
        {
            new ProductSale(ProductId: 10, Quantity: 2, NetAmount: 200, Profit: 40),
            new ProductSale(ProductId: 20, Quantity: 1, NetAmount: 450, Profit: 90),
            new ProductSale(ProductId: 10, Quantity: 3, NetAmount: 300, Profit: 60),
        };
        var top = SalesAnalytics.TopProducts(items, take: 10);
        Assert.Equal(10, top[0].Id);                 // 200+300 = 500 (بیشترین)
        Assert.Equal(20, top[1].Id);                 // 450
        Assert.Equal(100, top.First(r => r.Id == 10).Extra); // سود 40+60
        Assert.Equal(190, SalesAnalytics.TotalProfit(items));
    }

    [Theory]
    [InlineData("1404/03/20", "1404/03")]
    [InlineData("", "")]
    [InlineData("baddate", "")]
    public void MonthKey_Handles_Valid_And_Invalid(string input, string expected)
        => Assert.Equal(expected, SalesAnalytics.MonthKey(input));
}
