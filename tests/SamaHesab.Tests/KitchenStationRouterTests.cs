using System.Collections.Generic;
using System.Linq;
using SamaHesab.Modules.Restaurant.Application;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>REST-PRINT-STATIONS — مسیریابیِ ردیف‌های سفارش به ایستگاه‌های چاپ.</summary>
public class KitchenStationRouterTests
{
    private static readonly List<StationDef> Stations = new()
    {
        new(1, "آشپزخانه", "KitchenPrn", true),
        new(2, "سالادبار", "SaladPrn", false),
        new(3, "پنتری",   "PantryPrn", false),
    };

    private static StationLine L(int prod, string name) => new(prod, name, 1, null);

    [Fact]
    public void Routes_Each_Item_To_Its_Mapped_Station()
    {
        var map = new Dictionary<int, int> { [10] = 2, [20] = 3 };   // کالا۱۰→سالادبار، کالا۲۰→پنتری
        var lines = new[] { L(10, "سالادِ فصل"), L(20, "نوشابه"), L(30, "جوجه‌کباب") };

        var tickets = KitchenStationRouter.Route(lines, map, Stations);

        // جوجه (بدونِ نگاشت) → ایستگاهِ پیش‌فرض (آشپزخانه).
        Assert.Equal(3, tickets.Count);
        Assert.Equal("SaladPrn", tickets.Single(t => t.StationName == "سالادبار").PrinterName);
        Assert.Contains("جوجه‌کباب", tickets.Single(t => t.StationName == "آشپزخانه").Lines.Select(l => l.Name));
        Assert.Contains("نوشابه", tickets.Single(t => t.StationName == "پنتری").Lines.Select(l => l.Name));
    }

    [Fact]
    public void Unmapped_Items_Go_To_Default_Station()
    {
        var tickets = KitchenStationRouter.Route(new[] { L(99, "آبِ معدنی") }, new Dictionary<int, int>(), Stations);
        var t = Assert.Single(tickets);
        Assert.Equal("آشپزخانه", t.StationName);          // پیش‌فرض
        Assert.Equal("KitchenPrn", t.PrinterName);
    }

    [Fact]
    public void Without_Default_Station_Unmapped_Goes_To_Virtual_Group()
    {
        var noDefault = new List<StationDef> { new(2, "سالادبار", "SaladPrn", false) };
        var tickets = KitchenStationRouter.Route(new[] { L(99, "نان") }, new Dictionary<int, int>(), noDefault);
        var t = Assert.Single(tickets);
        Assert.Equal("بدون ایستگاه", t.StationName);
        Assert.Equal("", t.PrinterName);                  // پرینترِ خالی → پیش‌فرضِ سیستم
    }
}
