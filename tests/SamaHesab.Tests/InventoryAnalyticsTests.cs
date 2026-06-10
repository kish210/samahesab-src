using SamaHesab.Application.BI;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>موتور روند موجودی — تجمیع ماهانه‌ی ورود/خروج کاردکس.</summary>
public class InventoryAnalyticsTests
{
    [Fact]
    public void MonthlyMovement_Splits_In_And_Out()
    {
        var moves = new[]
        {
            new StockMovement("1404/01/05", 100),  // ورود
            new StockMovement("1404/01/20", -30),  // خروج
            new StockMovement("1404/02/02", -10),  // خروج
            new StockMovement("1404/02/10", 50),   // ورود
        };
        var trend = InventoryAnalytics.MonthlyMovement(moves);

        Assert.Equal(2, trend.Count);
        Assert.Equal("1404/01", trend[0].Period);
        Assert.Equal(100, trend[0].InQty);
        Assert.Equal(30, trend[0].OutQty);
        Assert.Equal(70, trend[0].Net);

        Assert.Equal("1404/02", trend[1].Period);
        Assert.Equal(50, trend[1].InQty);
        Assert.Equal(10, trend[1].OutQty);
        Assert.Equal(40, trend[1].Net);
    }

    [Fact]
    public void Ignores_Invalid_Dates()
    {
        var trend = InventoryAnalytics.MonthlyMovement(new[] { new StockMovement("bad", 5) });
        Assert.Empty(trend);
    }
}
