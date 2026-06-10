using SamaHesab.Domain.Entities.POS;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>کار #۳۰ — شیفت/صندوق POS (منطق دامنه).</summary>
public class CashShiftTests
{
    private static CashShift Open(decimal floatAmt = 1_000_000) => CashShift.Open(1, 1, 7, floatAmt);

    [Fact]
    public void Open_Starts_Empty_And_Open()
    {
        var s = Open();
        Assert.True(s.IsOpen);
        Assert.Equal(0, s.SalesCount);
        Assert.Equal(1_000_000, s.OpeningFloat);
    }

    [Fact]
    public void RecordSale_Accumulates_Cash_And_Card()
    {
        var s = Open();
        s.RecordSale(500_000, isCash: true);
        s.RecordSale(300_000, isCash: false);
        s.RecordSale(200_000, isCash: true);
        Assert.Equal(700_000, s.CashSales);
        Assert.Equal(300_000, s.CardSales);
        Assert.Equal(3, s.SalesCount);
    }

    [Fact]
    public void Close_Computes_Expected_And_Variance()
    {
        var s = Open(1_000_000);
        s.RecordSale(700_000, true);            // expected cash = 1,700,000
        s.Close(countedCash: 1_650_000);        // کسری ۵۰٬۰۰۰
        Assert.False(s.IsOpen);
        Assert.Equal(1_700_000, s.ExpectedCash);
        Assert.Equal(-50_000, s.Variance);
    }

    [Fact]
    public void Cannot_Close_Twice()
    {
        var s = Open();
        s.Close(1_000_000);
        Assert.Throws<InvalidOperationException>(() => s.Close(1_000_000));
    }

    [Fact]
    public void Cannot_Record_After_Close()
    {
        var s = Open();
        s.Close(1_000_000);
        Assert.Throws<InvalidOperationException>(() => s.RecordSale(100_000, true));
    }
}
