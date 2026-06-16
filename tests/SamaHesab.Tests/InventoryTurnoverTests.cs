using SamaHesab.Application.Reports;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>فاز ۱۲ (پولیش) — گردشِ موجودی.</summary>
public class InventoryTurnoverTests
{
    [Fact]
    public void Ratio_and_days_for_normal_case()
    {
        var r = InventoryTurnover.Compute(cogs: 300, inventoryValue: 100, periodDays: 90);
        Assert.Equal(3m, r.Ratio);          // ۳ بار گردش در دوره
        Assert.Equal(30m, r.DaysOnHand);    // ۹۰×۱۰۰÷۳۰۰ = ۳۰ روز ماندگاری
    }

    [Fact]
    public void No_sales_marks_idle_minus_one()
    {
        var r = InventoryTurnover.Compute(cogs: 0, inventoryValue: 50, periodDays: 90);
        Assert.Equal(0m, r.Ratio);
        Assert.Equal(-1m, r.DaysOnHand);    // بی‌گردش
    }

    [Fact]
    public void Zero_inventory_is_zero()
    {
        var r = InventoryTurnover.Compute(cogs: 100, inventoryValue: 0, periodDays: 90);
        Assert.Equal(0m, r.Ratio);
        Assert.Equal(0m, r.DaysOnHand);
    }
}
