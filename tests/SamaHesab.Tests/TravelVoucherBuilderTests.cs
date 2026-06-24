using System.Collections.Generic;
using System.Linq;
using SamaHesab.Modules.Tourism.Application;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>سازندهٔ واچرِ سفر — خدمات با تاریخِ سفر، فهرستِ مسافران، جمعِ مبالغ.</summary>
public class TravelVoucherBuilderTests
{
    private static VoucherHeader H() => new(
        SaleId: 5, VoucherNo: "TUR-100", IssueDate: "1405/03/20",
        CustomerName: "آقای رضایی", SalespersonName: "خانم احمدی", PaymentMethod: "نقدی");

    private static VoucherServiceLine Line(string name, string? travel, decimal qty, decimal price, params string[] pax)
        => new(name, "آژانس البرز", travel, qty, price,
            pax.Select(p => new VoucherPassenger(p, null, null)).ToList());

    [Fact]
    public void Aggregates_Totals_And_Passenger_Count()
    {
        var v = TravelVoucherBuilder.Build(H(), new[]
        {
            Line("تور کیش", "1405/04/01", 2, 5_000_000, "رضایی", "همسر"),
            Line("بیمهٔ مسافرتی", null, 2, 300_000),
        });

        Assert.Equal(10_600_000, v.TotalSale);   // 2*5,000,000 + 2*300,000
        Assert.Equal(10_600_000, v.NetPayable);  // بدونِ تخفیف
        Assert.Equal(2, v.PassengerCount);
        Assert.Equal(2, v.Lines.Count);
        Assert.Equal("1405/04/01", v.Lines[0].TravelDate);
    }

    [Fact]
    public void Discount_Reduces_NetPayable()
    {
        var v = TravelVoucherBuilder.Build(H(),
            new[] { Line("تور", "1405/04/01", 1, 1_000_000) }, totalDiscount: 150_000);

        Assert.Equal(1_000_000, v.TotalSale);
        Assert.Equal(150_000, v.TotalDiscount);
        Assert.Equal(850_000, v.NetPayable);
    }

    [Fact]
    public void Negative_Discount_Is_Clamped()
    {
        var v = TravelVoucherBuilder.Build(H(),
            new[] { Line("تور", null, 1, 500_000) }, totalDiscount: -50_000);
        Assert.Equal(0, v.TotalDiscount);
        Assert.Equal(500_000, v.NetPayable);
    }

    [Fact]
    public void Header_Carried_Through()
    {
        var v = TravelVoucherBuilder.Build(H(), new[] { Line("تور", null, 1, 1) });
        Assert.Equal("TUR-100", v.Header.VoucherNo);
        Assert.Equal("آقای رضایی", v.Header.CustomerName);
    }
}
