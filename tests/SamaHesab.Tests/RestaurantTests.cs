using SamaHesab.Modules.Restaurant.Domain;
using SamaHesab.Domain.Enums;
using SamaHesab.Domain.Events;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>جریان سفارش رستوران (فاز ۱ نسخه ۲): افزودن آیتم، ارسال به آشپزخانه، تسویه.</summary>
public class RestaurantTests
{
    private static RestaurantOrder DineInOrder() =>
        RestaurantOrder.Create(companyId: 1, branchId: 1, orderNumber: "S1",
            orderType: RestaurantOrderType.DineIn, tableId: 5, guestCount: 2);

    private static RestaurantOrderItem Item(string name, decimal qty, decimal price) =>
        RestaurantOrderItem.Create(companyId: 1, productId: 1, productName: name, quantity: qty, unitPrice: price);

    [Fact]
    public void DineIn_Requires_Table()
        => Assert.Throws<ArgumentException>(() =>
            RestaurantOrder.Create(1, 1, "S1", RestaurantOrderType.DineIn, tableId: null));

    [Fact]
    public void Adding_Items_Updates_Totals()
    {
        var o = DineInOrder();
        o.AddItem(Item("چلوکباب", 2, 1000));
        o.AddItem(Item("دوغ", 3, 100));

        Assert.Equal(2300, o.SubTotal);     // 2*1000 + 3*100
        Assert.Equal(2300, o.GrandTotal);
    }

    [Fact]
    public void SendToKitchen_Marks_Items_And_Raises_Event()
    {
        var o = DineInOrder();
        o.AddItem(Item("چلوکباب", 1, 1000));

        var sent = o.SendToKitchen(kitchenTicketId: 42);

        Assert.Single(sent);
        Assert.Equal(RestaurantOrderStatus.Sent, o.Status);
        Assert.All(o.Items, i => Assert.Equal(OrderItemStatus.InKitchen, i.Status));
        Assert.All(o.Items, i => Assert.Equal(42, i.KitchenTicketId));
        Assert.Contains(o.DomainEvents, e => e is OrderSentToKitchenEvent);
    }

    [Fact]
    public void SendToKitchen_Throws_When_Nothing_Pending()
    {
        var o = DineInOrder();
        o.AddItem(Item("چلوکباب", 1, 1000));
        o.SendToKitchen(1);
        Assert.Throws<InvalidOperationException>(() => o.SendToKitchen(2));
    }

    [Fact]
    public void Settle_Sets_Status_And_Raises_Event()
    {
        var o = DineInOrder();
        o.AddItem(Item("چلوکباب", 2, 1000));
        o.SetCharges(discount: 0, serviceCharge: 200, tax: 180, tip: 0);

        o.Settle(userId: 3, paidAmount: 2380);

        Assert.Equal(RestaurantOrderStatus.Settled, o.Status);
        Assert.Equal(2380, o.GrandTotal);   // 2000 + 200 + 180
        Assert.Contains(o.DomainEvents, e => e is RestaurantOrderSettledEvent);
    }

    [Fact]
    public void Settled_Order_Cannot_Be_Edited()
    {
        var o = DineInOrder();
        o.AddItem(Item("چلوکباب", 1, 1000));
        o.Settle(1, 1000);
        Assert.Throws<InvalidOperationException>(() => o.AddItem(Item("دوغ", 1, 100)));
    }

    // ─── #34: ویرایش تعداد / حذف / یادداشت ردیف ───
    [Fact]
    public void ChangeItemQuantity_Updates_Totals()
    {
        var o = DineInOrder();
        o.AddItem(Item("چلوکباب", 2, 1000));   // Id=0 در تست دامنه
        o.ChangeItemQuantity(itemId: 0, quantity: 5);
        Assert.Equal(5000, o.SubTotal);
        Assert.Equal(5000, o.GrandTotal);
    }

    [Fact]
    public void ChangeItemQuantity_To_Zero_Removes_The_Row()
    {
        var o = DineInOrder();
        o.AddItem(Item("چلوکباب", 2, 1000));
        o.ChangeItemQuantity(itemId: 0, quantity: 0);
        Assert.Empty(o.Items);
        Assert.Equal(0, o.GrandTotal);
    }

    [Fact]
    public void RemoveItem_Deletes_Row_And_Recalculates()
    {
        var o = DineInOrder();
        o.AddItem(Item("چلوکباب", 1, 1000));
        o.RemoveItem(itemId: 0);
        Assert.Empty(o.Items);
        Assert.Equal(0, o.GrandTotal);
    }

    [Fact]
    public void SetItemNotes_Updates_Note()
    {
        var o = DineInOrder();
        o.AddItem(Item("چلوکباب", 1, 1000));
        o.SetItemNotes(itemId: 0, notes: "بدون پیاز");
        Assert.Equal("بدون پیاز", System.Linq.Enumerable.First(o.Items).Notes);
    }

    [Fact]
    public void ChangeItemQuantity_Throws_After_SentToKitchen()
    {
        var o = DineInOrder();
        o.AddItem(Item("چلوکباب", 1, 1000));
        o.SendToKitchen(1);
        // ردیف ارسال‌شده دیگر Pending نیست → ویرایش تعداد مجاز نیست
        Assert.Throws<InvalidOperationException>(() => o.ChangeItemQuantity(itemId: 0, quantity: 3));
    }

    // ─── #36: انتقال میز ───
    [Fact]
    public void MoveToTable_Changes_TableId()
    {
        var o = DineInOrder();   // tableId=5
        o.MoveToTable(7);
        Assert.Equal(7, o.TableId);
    }

    [Fact]
    public void MoveToTable_Throws_When_Settled()
    {
        var o = DineInOrder();
        o.AddItem(Item("چلوکباب", 1, 1000));
        o.Settle(1, 1000);
        Assert.Throws<InvalidOperationException>(() => o.MoveToTable(7));
    }

    [Fact]
    public void AssignWaiter_Sets_Waiter()
    {
        var o = DineInOrder();
        o.AssignWaiter(42);
        Assert.Equal(42, o.WaiterId);
    }
}
