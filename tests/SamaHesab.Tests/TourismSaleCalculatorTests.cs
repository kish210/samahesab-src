using SamaHesab.Modules.Tourism.Application;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>TUR-C2-2 — موتورِ خالصِ سود/برداشتِ ودیعه.</summary>
public class TourismSaleCalculatorTests
{
    [Fact]
    public void Line_Computes_Net_Cost_Profit()
    {
        var r = TourismSaleCalculator.ComputeLine(
            new TourismSaleLineInput(Quantity: 2, UnitPrice: 1_000_000, UnitCost: 700_000, DiscountAmount: 100_000));
        Assert.Equal(2_000_000m, r.Gross);
        Assert.Equal(100_000m, r.Discount);
        Assert.Equal(1_900_000m, r.NetSale);
        Assert.Equal(1_400_000m, r.Cost);
        Assert.Equal(500_000m, r.Profit);   // ۱٬۹۰۰٬۰۰۰ − ۱٬۴۰۰٬۰۰۰
    }

    [Fact]
    public void Totals_Sum_Lines_And_Drawdown_Equals_Cost()
    {
        var lines = new[]
        {
            new TourismSaleLineInput(1, 1_000_000, 700_000),
            new TourismSaleLineInput(2, 500_000, 300_000, DiscountAmount: 50_000),
        };
        var t = TourismSaleCalculator.ComputeTotals(lines);
        Assert.Equal(2_000_000m, t.Gross);          // ۱م + ۱م
        Assert.Equal(50_000m, t.Discount);
        Assert.Equal(1_950_000m, t.NetSale);
        Assert.Equal(1_300_000m, t.Cost);           // ۷۰۰ک + ۶۰۰ک
        Assert.Equal(650_000m, t.Profit);
        Assert.Equal(t.Cost, t.DepositDrawdown);    // برداشتِ ودیعه = بهای تمام‌شده
    }

    [Fact]
    public void Empty_Sale_Is_Zero()
    {
        var t = TourismSaleCalculator.ComputeTotals(System.Array.Empty<TourismSaleLineInput>());
        Assert.Equal(0m, t.NetSale);
        Assert.Equal(0m, t.DepositDrawdown);
    }

    [Fact]
    public void Deposit_Balance_And_Sufficiency()
    {
        var bal = TourismSaleCalculator.DepositBalance(totalCharged: 10_000_000, totalDrawn: 6_500_000);
        Assert.Equal(3_500_000m, bal);
        Assert.True(TourismSaleCalculator.HasSufficientDeposit(bal, 3_500_000));
        Assert.False(TourismSaleCalculator.HasSufficientDeposit(bal, 3_500_001));
    }
}
