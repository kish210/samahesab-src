using SamaHesab.Domain.Entities.Purchase;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>منطق دامنه‌ی سفارش خرید.</summary>
public class PurchaseOrderTests
{
    [Fact]
    public void AddItem_Accumulates_Total()
    {
        var po = PurchaseOrder.Create(1, 1, "PO-0001", "1404/03/10", supplierId: 5, source: "خودکار");
        po.AddItem(productId: 10, quantity: 3, unitPrice: 1000);
        po.AddItem(productId: 20, quantity: 2, unitPrice: 2500);

        Assert.Equal(2, po.Items.Count);
        Assert.Equal(3 * 1000 + 2 * 2500, po.Total);   // 8000
        Assert.Equal(1, po.Items.First().RowNumber);
        Assert.Equal("خودکار", po.Source);
        Assert.Equal("پیش‌نویس", po.StatusCode);
    }

    [Fact]
    public void Approve_Requires_Items()
    {
        var po = PurchaseOrder.Create(1, 1, "PO-0002", "1404/03/10");
        Assert.Throws<InvalidOperationException>(() => po.Approve());

        po.AddItem(1, 1, 100);
        po.Approve();
        Assert.Equal("تأییدشده", po.StatusCode);
    }

    [Fact]
    public void Cannot_Approve_Twice()
    {
        var po = PurchaseOrder.Create(1, 1, "PO-0003", "1404/03/10");
        po.AddItem(1, 1, 100);
        po.Approve();
        Assert.Throws<InvalidOperationException>(() => po.Approve());
    }

    [Fact]
    public void AddItem_Rejects_NonPositive_Quantity()
    {
        var po = PurchaseOrder.Create(1, 1, "PO-0004", "1404/03/10");
        Assert.Throws<ArgumentException>(() => po.AddItem(1, 0, 100));
    }
}
