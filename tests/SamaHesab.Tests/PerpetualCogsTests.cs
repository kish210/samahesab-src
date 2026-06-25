using System.Linq;
using SamaHesab.Application.Inventory;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>INV-1 گام۴ — ثبتِ دائمیِ بهای تمام‌شده: ردیف‌های سند متوازن و در جهتِ درست.</summary>
public class PerpetualCogsTests
{
    private const int Cogs = 71;        // 7-01-001
    private const int Inventory = 15;   // 1-05-001

    [Fact]
    public void Sale_Debits_Cogs_Credits_Inventory_Balanced()
    {
        var lines = PerpetualCogs.Build(1_200_000, Cogs, Inventory, reverse: false);

        Assert.Equal(2, lines.Count);
        var cogs = lines.Single(l => l.AccountId == Cogs);
        var inv = lines.Single(l => l.AccountId == Inventory);
        Assert.Equal(1_200_000, cogs.Debit);   // بهای تمام‌شده بدهکار
        Assert.Equal(0, cogs.Credit);
        Assert.Equal(1_200_000, inv.Credit);    // موجودی کالا بستانکار
        Assert.Equal(0, inv.Debit);
        Assert.Equal(lines.Sum(l => l.Debit), lines.Sum(l => l.Credit));   // توازن
    }

    [Fact]
    public void SaleReturn_Reverses_Direction()
    {
        var lines = PerpetualCogs.Build(500_000, Cogs, Inventory, reverse: true);

        var cogs = lines.Single(l => l.AccountId == Cogs);
        var inv = lines.Single(l => l.AccountId == Inventory);
        Assert.Equal(500_000, inv.Debit);       // موجودی بازمی‌گردد ⇒ بدهکار
        Assert.Equal(500_000, cogs.Credit);     // معکوسِ بهای تمام‌شده ⇒ بستانکار
        Assert.Equal(lines.Sum(l => l.Debit), lines.Sum(l => l.Credit));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    public void NonPositive_Cost_Yields_No_Lines(decimal cost)
        => Assert.Empty(PerpetualCogs.Build(cost, Cogs, Inventory, reverse: false));

    [Fact]
    public void Amount_Is_Rounded_To_Two_Decimals()
    {
        var line = PerpetualCogs.Build(99.999m, Cogs, Inventory, reverse: false)
            .Single(l => l.AccountId == Cogs);
        Assert.Equal(100.00m, line.Debit);
    }
}
