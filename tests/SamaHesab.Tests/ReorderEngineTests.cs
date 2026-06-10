using SamaHesab.Application.Automation;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>موتور پیشنهاد خودکار سفارش خرید.</summary>
public class ReorderEngineTests
{
    [Fact]
    public void Suggests_Up_To_MaxStock_When_Below_Threshold()
    {
        var s = ReorderEngine.Suggest(new[]
        {
            new ReorderInput(1, "روغن", OnHand: 5, MinStock: 3, ReorderPoint: 10, MaxStock: 30),
        });
        Assert.Single(s);
        Assert.Equal(10, s[0].Threshold);
        Assert.Equal(25, s[0].SuggestedQty);   // 30 - 5
    }

    [Fact]
    public void Falls_Back_To_Double_Threshold_When_No_MaxStock()
    {
        var s = ReorderEngine.Suggest(new[]
        {
            new ReorderInput(1, "برنج", OnHand: 2, MinStock: 4, ReorderPoint: null, MaxStock: null),
        });
        Assert.Equal(4, s[0].Threshold);
        Assert.Equal(6, s[0].SuggestedQty);    // 4*2 - 2
    }

    [Fact]
    public void Skips_Sufficient_Stock_And_Zero_Threshold()
    {
        var s = ReorderEngine.Suggest(new[]
        {
            new ReorderInput(1, "نمک", OnHand: 50, MinStock: 5, ReorderPoint: 10, MaxStock: 40), // کافی
            new ReorderInput(2, "شکر", OnHand: 1,  MinStock: 0, ReorderPoint: null, MaxStock: null), // بدون آستانه
        });
        Assert.Empty(s);
    }

    [Fact]
    public void Orders_Most_Urgent_First()
    {
        var s = ReorderEngine.Suggest(new[]
        {
            new ReorderInput(1, "A", OnHand: 9, MinStock: 0, ReorderPoint: 10, MaxStock: 20), // کسری 10%
            new ReorderInput(2, "B", OnHand: 0, MinStock: 0, ReorderPoint: 10, MaxStock: 20), // کسری 100%
        });
        Assert.Equal(2, s[0].ProductId);   // فوری‌ترین
        Assert.Equal(1, s[1].ProductId);
    }
}
