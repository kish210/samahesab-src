using System.Collections.Generic;
using System.Linq;
using SamaHesab.Application.Common.Security;
using SamaHesab.Application.Tourism;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>ظرفیت/موجودیِ محصولاتِ گردشگری برای نمای فروشنده + مجوزهای RBAC.</summary>
public class TourismAvailabilityTests
{
    private static TourismProductInput P(int id, string name, decimal price, int? cap, bool active = true)
        => new(id, name, price, cap, active);

    [Fact]
    public void Remaining_Is_Capacity_Minus_Sold()
    {
        var rows = TourismAvailability.Build(
            new[] { P(1, "تور کیش", 5_000_000, 20) },
            new Dictionary<int, decimal> { [1] = 7 });

        var r = Assert.Single(rows);
        Assert.Equal(20, r.Capacity);
        Assert.Equal(7, r.Sold);
        Assert.Equal(13, r.Remaining);
        Assert.False(r.IsSoldOut);
        Assert.False(r.Unlimited);
    }

    [Fact]
    public void Null_Capacity_Means_Unlimited_Never_SoldOut()
    {
        var rows = TourismAvailability.Build(
            new[] { P(1, "بیمهٔ مسافرتی", 300_000, null) },
            new Dictionary<int, decimal> { [1] = 999 });

        var r = Assert.Single(rows);
        Assert.True(r.Unlimited);
        Assert.Null(r.Remaining);
        Assert.False(r.IsSoldOut);
    }

    [Fact]
    public void SoldOut_When_Sold_Reaches_Capacity_And_Remaining_Not_Negative()
    {
        var rows = TourismAvailability.Build(
            new[] { P(1, "گشت جزیره", 1_000_000, 10) },
            new Dictionary<int, decimal> { [1] = 12 });

        var r = Assert.Single(rows);
        Assert.True(r.IsSoldOut);
        Assert.Equal(0, r.Remaining);   // منفی نمی‌شود
    }

    [Fact]
    public void Inactive_Products_Excluded_By_Default()
    {
        var rows = TourismAvailability.Build(
            new[] { P(1, "فعال", 1, 5), P(2, "غیرفعال", 1, 5, active: false) },
            new Dictionary<int, decimal>());

        Assert.Single(rows);
        Assert.Equal(1, rows[0].ProductId);
    }

    [Fact]
    public void Product_With_No_Sales_Has_Full_Remaining()
    {
        var rows = TourismAvailability.Build(
            new[] { P(1, "تور", 1, 8) },
            new Dictionary<int, decimal>());
        Assert.Equal(0, rows[0].Sold);
        Assert.Equal(8, rows[0].Remaining);
    }

    [Theory]
    [InlineData("Tourism.Manage")]
    [InlineData("Tourism.Sell")]
    [InlineData("Tourism.View")]
    public void Tourism_Permissions_Exist_In_Catalog(string code)
        => Assert.Contains(PermissionCatalog.All, p => p.Code == code);

    [Fact]
    public void Seller_Granted_Sell_But_Not_Manage()
    {
        var granted = new[] { "Tourism.Sell", "Tourism.View" };
        Assert.True(PermissionCatalog.Grants(granted, "Tourism.Sell"));
        Assert.False(PermissionCatalog.Grants(granted, "Tourism.Manage"));
    }
}
