using System;
using System.Linq;
using SamaHesab.Application.Common.Security;
using SamaHesab.Application.Restaurant;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>زمانِ انتظارِ میز (رنگ‌بندیِ نقشهٔ میز) + مجوزهای نقشیِ رستوران.</summary>
public class TableWaitTimeTests
{
    private static readonly DateTime Now = new(2026, 6, 24, 20, 0, 0);

    [Fact]
    public void Table_Without_Open_Order_Is_Free()
    {
        var rows = TableWaitTime.Build(
            new[] { new TableWaitInput(1, "میز ۱", null, HasOpenOrder: false) }, Now);
        var r = Assert.Single(rows);
        Assert.False(r.Occupied);
        Assert.Equal(0, r.ElapsedMinutes);
        Assert.Equal(TableWaitState.Free, r.State);
    }

    [Theory]
    [InlineData(10, TableWaitState.Normal)]
    [InlineData(35, TableWaitState.Warning)]
    [InlineData(75, TableWaitState.Critical)]
    public void State_By_Elapsed_Thresholds(int minutesAgo, TableWaitState expected)
    {
        var rows = TableWaitTime.Build(
            new[] { new TableWaitInput(1, "میز ۱", Now.AddMinutes(-minutesAgo), HasOpenOrder: true) }, Now);
        var r = Assert.Single(rows);
        Assert.True(r.Occupied);
        Assert.Equal(minutesAgo, r.ElapsedMinutes);
        Assert.Equal(expected, r.State);
    }

    [Fact]
    public void Custom_Thresholds_Respected()
    {
        var rows = TableWaitTime.Build(
            new[] { new TableWaitInput(1, "میز ۱", Now.AddMinutes(-20), HasOpenOrder: true) },
            Now, warningMinutes: 15, criticalMinutes: 25);
        Assert.Equal(TableWaitState.Warning, rows[0].State);
    }

    [Fact]
    public void Future_OpenedAt_Clamps_To_Zero_Without_Negative()
    {
        var rows = TableWaitTime.Build(
            new[] { new TableWaitInput(1, "میز ۱", Now.AddMinutes(5), HasOpenOrder: true) }, Now);
        Assert.Equal(0, rows[0].ElapsedMinutes);
        Assert.Equal(TableWaitState.Normal, rows[0].State);
    }

    [Theory]
    [InlineData("Restaurant.Operate")]
    [InlineData("Restaurant.Cashier")]
    [InlineData("Restaurant.Kitchen")]
    [InlineData("Restaurant.Manage")]
    public void Restaurant_Permissions_Exist(string code)
        => Assert.Contains(PermissionCatalog.All, p => p.Code == code);

    [Fact]
    public void Waiter_Cannot_Settle_Bill()
    {
        var granted = new[] { "Restaurant.Operate" };
        Assert.True(PermissionCatalog.Grants(granted, "Restaurant.Operate"));
        Assert.False(PermissionCatalog.Grants(granted, "Restaurant.Cashier"));
    }
}
