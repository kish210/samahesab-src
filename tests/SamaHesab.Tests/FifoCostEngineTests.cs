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

    // U10 — قراردادِ «بهای نهاییِ یک خروج» که CreateSalesInvoiceCommand استفاده می‌کند:
    // marginal = Compute(prior + thisIssue).IssuedCost − Compute(prior).IssuedCost
    [Fact]
    public void Marginal_Issue_Cost_Spans_Layers()
    {
        var prior = new System.Collections.Generic.List<(decimal, decimal)> { (10, 100), (10, 200) };
        var before = FifoCostEngine.Compute(prior).IssuedCost;          // ۰ (هنوز خروجی نیست)
        var after = FifoCostEngine.Compute(new[] { (10m, 100m), (10m, 200m), (-15m, 0m) }).IssuedCost;
        var marginal = after - before;                                  // 10*100 + 5*200 = 2000
        Assert.Equal(2000, marginal);
        Assert.Equal(133.3333m, System.Math.Round(marginal / 15m, 4));  // بهای واحدِ خروج (همانندِ command)
    }

    [Fact]
    public void Marginal_Issue_Cost_After_Prior_Issue()
    {
        // قبلاً ۳ واحد خارج شده؛ خروجِ جدیدِ ۹ واحد باید از لایه‌های باقی‌مانده محاسبه شود.
        var prior = new[] { (10m, 100m), (10m, 200m), (-3m, 0m) };       // issued=300، باقی: 7@100 + 10@200
        var before = FifoCostEngine.Compute(prior).IssuedCost;          // 300
        var with = new System.Collections.Generic.List<(decimal, decimal)>(prior) { (-9m, 0m) };
        var after = FifoCostEngine.Compute(with).IssuedCost;            // 300 + (7*100 + 2*200) = 300+1100
        Assert.Equal(1100, after - before);                            // بهای ۹ واحدِ جدید
    }
}
