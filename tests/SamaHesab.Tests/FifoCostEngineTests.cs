using SamaHesab.Application.Inventory;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>کار #۲۹ — موتور بهای تمام‌شده‌ی FIFO (منطق خالص).</summary>
public class FifoCostEngineTests
{
    [Fact]
    public void Fifo_Issues_From_Oldest_Layer_First()
    {
        // ورود ۱۰@۱۰۰، ورود ۱۰@۱۲۰، خروج ۱۵
        var v = FifoCostEngine.Compute(new (decimal, decimal)[] { (10, 100), (10, 120), (-15, 0) });
        Assert.Equal(1600, v.IssuedCost);      // 10*100 + 5*120
        Assert.Equal(5, v.RemainingQty);
        Assert.Equal(600, v.RemainingValue);   // 5*120
        Assert.Single(v.RemainingLayers);
    }

    [Fact]
    public void Remaining_Keeps_Multiple_Layers()
    {
        var v = FifoCostEngine.Compute(new (decimal, decimal)[] { (10, 100), (5, 200), (-3, 0) });
        Assert.Equal(300, v.IssuedCost);       // 3*100
        Assert.Equal(12, v.RemainingQty);
        Assert.Equal(1700, v.RemainingValue);  // 7*100 + 5*200
        Assert.Equal(2, v.RemainingLayers.Count);
    }

    [Fact]
    public void Over_Issue_Uses_Last_Cost_For_Shortfall()
    {
        // خروج بیش از موجودیِ ثبت‌شده → کسری با آخرین بها
        var v = FifoCostEngine.Compute(new (decimal, decimal)[] { (5, 100), (-8, 0) });
        Assert.Equal(800, v.IssuedCost);       // 5*100 + 3*100 (last cost)
        Assert.Equal(0, v.RemainingQty);
        Assert.Empty(v.RemainingLayers);
    }

    [Fact]
    public void Empty_Movements_Is_Zero()
    {
        var v = FifoCostEngine.Compute(System.Array.Empty<(decimal, decimal)>());
        Assert.Equal(0, v.IssuedCost);
        Assert.Equal(0, v.RemainingQty);
    }
}
