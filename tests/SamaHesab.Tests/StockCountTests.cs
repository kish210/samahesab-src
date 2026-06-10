using SamaHesab.Domain.Entities.Inventory;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>انبارگردانی سندی (#۲۸) — منطق دامنه.</summary>
public class StockCountTests
{
    private static StockCountSession Session()
    {
        var s = StockCountSession.Create(companyId: 1, branchId: 1, warehouseId: 1, date: "1404/07/01");
        s.AddLine(StockCountLine.Create(0, productId: 10, "روغن موتور", systemQty: 20));
        s.AddLine(StockCountLine.Create(0, productId: 11, "فیلتر", systemQty: 5));
        return s;
    }

    [Fact]
    public void New_Line_Defaults_Counted_To_System_With_Zero_Variance()
    {
        var l = StockCountLine.Create(0, 10, "x", 20);
        Assert.Equal(20, l.CountedQty);
        Assert.Equal(0, l.Variance);
    }

    [Fact]
    public void Variance_Reflects_Counted_Minus_System()
    {
        var s = Session();
        var line = s.Lines.First(l => l.ProductId == 10);
        line.SetCounted(18);   // کسری ۲
        Assert.Equal(-2, line.Variance);
        Assert.Single(s.VarianceLines());
    }

    [Fact]
    public void Post_Marks_Session_And_Blocks_Reposting()
    {
        var s = Session();
        s.Post();
        Assert.True(s.IsPosted);
        Assert.Throws<InvalidOperationException>(() => s.Post());
    }

    [Fact]
    public void Posted_Session_Rejects_New_Lines()
    {
        var s = Session();
        s.Post();
        Assert.Throws<InvalidOperationException>(() => s.AddLine(StockCountLine.Create(0, 12, "y", 1)));
    }

    [Fact]
    public void SetCounted_Rejects_Negative()
        => Assert.Throws<ArgumentException>(() => StockCountLine.Create(0, 10, "x", 5).SetCounted(-1));
}
