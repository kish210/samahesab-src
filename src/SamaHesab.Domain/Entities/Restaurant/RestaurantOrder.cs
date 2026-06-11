using SamaHesab.Domain.Common;
using SamaHesab.Domain.Enums;
using SamaHesab.Domain.Events;

namespace SamaHesab.Domain.Entities.Restaurant;

/// <summary>سفارش رستوران. هسته‌ی فرایند: باز شدن روی میز → افزودن آیتم → ارسال به آشپزخانه → سرو → تسویه.
/// برخلاف فاکتور فروشگاهی، سفارش رستوران «باز» می‌ماند و به مرور آیتم می‌گیرد.</summary>
public class RestaurantOrder : AuditableEntity
{
    public int BranchId { get; private set; }
    public string OrderNumber { get; private set; } = default!;
    public RestaurantOrderType OrderType { get; private set; }
    public RestaurantOrderStatus Status { get; private set; } = RestaurantOrderStatus.Open;
    public int? TableId { get; private set; }           // برای DineIn
    public int GuestCount { get; private set; } = 1;
    public int? WaiterId { get; private set; }
    public int? CustomerId { get; private set; }        // برای Delivery/باشگاه مشتری
    public DateTime OpenedAt { get; private set; } = DateTime.Now;
    public DateTime? SettledAt { get; private set; }

    public decimal SubTotal { get; private set; }
    public decimal Discount { get; private set; }
    public decimal ServiceCharge { get; private set; }  // حق سرویس
    public decimal Tax { get; private set; }            // مالیات
    public decimal Tip { get; private set; }            // انعام
    public decimal GrandTotal { get; private set; }
    public decimal PaidAmount { get; private set; }
    public string? Description { get; private set; }
    public int? SalesInvoiceId { get; private set; }    // فاکتور فروش متناظر پس از تسویه (فاز بعد)

    public ICollection<RestaurantOrderItem> Items { get; private set; } = new List<RestaurantOrderItem>();

    private RestaurantOrder() { }

    public static RestaurantOrder Create(int companyId, int branchId, string orderNumber,
        RestaurantOrderType orderType, int? tableId = null, int guestCount = 1,
        int? waiterId = null, int? customerId = null)
    {
        if (string.IsNullOrWhiteSpace(orderNumber))
            throw new ArgumentException("شماره سفارش الزامی است.");
        if (orderType == RestaurantOrderType.DineIn && tableId is null)
            throw new ArgumentException("برای سفارش سالن، انتخاب میز الزامی است.");

        return new RestaurantOrder
        {
            CompanyId = companyId,
            BranchId = branchId,
            OrderNumber = orderNumber,
            OrderType = orderType,
            TableId = tableId,
            GuestCount = guestCount < 1 ? 1 : guestCount,
            WaiterId = waiterId,
            CustomerId = customerId
        };
    }

    public void AddItem(RestaurantOrderItem item)
    {
        EnsureEditable();
        Items.Add(item);
        Recalculate();
    }

    public void RemoveItem(int itemId)
    {
        EnsureEditable();
        var item = Items.FirstOrDefault(i => i.Id == itemId)
            ?? throw new InvalidOperationException("ردیف یافت نشد.");
        if (item.Status != OrderItemStatus.Pending)
            throw new InvalidOperationException("ردیف ارسال‌شده به آشپزخانه قابل حذف نیست (باید لغو شود).");
        Items.Remove(item);
        Recalculate();
    }

    /// <summary>تغییر تعداد یک ردیف؛ اگر به صفر یا کمتر برسد، ردیف حذف می‌شود. جمع سفارش بازمحاسبه می‌شود.</summary>
    public void ChangeItemQuantity(int itemId, decimal quantity)
    {
        EnsureEditable();
        var item = Items.FirstOrDefault(i => i.Id == itemId)
            ?? throw new InvalidOperationException("ردیف یافت نشد.");
        if (quantity <= 0) { RemoveItem(itemId); return; }
        item.ChangeQuantity(quantity);
        Recalculate();
    }

    /// <summary>یادداشت آشپزخانه‌ی یک ردیف را تنظیم می‌کند.</summary>
    public void SetItemNotes(int itemId, string? notes)
    {
        EnsureEditable();
        var item = Items.FirstOrDefault(i => i.Id == itemId)
            ?? throw new InvalidOperationException("ردیف یافت نشد.");
        item.SetNotes(notes);
    }

    /// <summary>انتقال سفارش به میز دیگر (آزادسازی میز قبلی و اشغال میز جدید در هندلر انجام می‌شود).</summary>
    public void MoveToTable(int newTableId)
    {
        if (Status is RestaurantOrderStatus.Settled or RestaurantOrderStatus.Cancelled)
            throw new InvalidOperationException("سفارش تسویه/لغوشده قابل انتقال نیست.");
        if (OrderType != RestaurantOrderType.DineIn)
            throw new InvalidOperationException("فقط سفارش سالن قابل انتقال میز است.");
        TableId = newTableId;
        UpdatedAt = DateTime.Now;
    }

    /// <summary>تخصیص/تغییر گارسون سفارش.</summary>
    public void AssignWaiter(int waiterId) { WaiterId = waiterId; UpdatedAt = DateTime.Now; }

    /// <summary>ردیف‌های در انتظار را به آشپزخانه ارسال می‌کند و رویداد ساخت رسید آشپزخانه را منتشر می‌کند.</summary>
    public IReadOnlyList<RestaurantOrderItem> SendToKitchen(int kitchenTicketId)
    {
        EnsureEditable();
        var pending = Items.Where(i => i.Status == OrderItemStatus.Pending).ToList();
        if (pending.Count == 0)
            throw new InvalidOperationException("ردیف جدیدی برای ارسال به آشپزخانه وجود ندارد.");
        foreach (var i in pending) i.AttachToKitchenTicket(kitchenTicketId);
        Status = RestaurantOrderStatus.Sent;
        UpdatedAt = DateTime.Now;
        AddDomainEvent(new OrderSentToKitchenEvent(Id, BranchId, kitchenTicketId));
        return pending;
    }

    public void SetCharges(decimal discount, decimal serviceCharge, decimal tax, decimal tip)
    {
        Discount = discount; ServiceCharge = serviceCharge; Tax = tax; Tip = tip;
        Recalculate();
    }

    public void Settle(int userId, decimal paidAmount)
    {
        if (Status is RestaurantOrderStatus.Settled or RestaurantOrderStatus.Cancelled)
            throw new InvalidOperationException("سفارش قابل تسویه نیست.");
        if (!Items.Any(i => i.Status != OrderItemStatus.Cancelled))
            throw new InvalidOperationException("سفارش خالی قابل تسویه نیست.");
        Recalculate();
        PaidAmount = paidAmount;
        Status = RestaurantOrderStatus.Settled;
        SettledAt = DateTime.Now;
        UpdatedAt = DateTime.Now;
        AddDomainEvent(new RestaurantOrderSettledEvent(Id, CompanyId, BranchId, GrandTotal, userId));
    }

    public void MarkServed()
    {
        if (Status == RestaurantOrderStatus.Sent) Status = RestaurantOrderStatus.Served;
        UpdatedAt = DateTime.Now;
    }

    public void Cancel()
    {
        if (Status == RestaurantOrderStatus.Settled)
            throw new InvalidOperationException("سفارش تسویه‌شده قابل لغو نیست.");
        Status = RestaurantOrderStatus.Cancelled;
        UpdatedAt = DateTime.Now;
    }

    public void LinkSalesInvoice(int salesInvoiceId) { SalesInvoiceId = salesInvoiceId; UpdatedAt = DateTime.Now; }

    private void EnsureEditable()
    {
        if (Status is RestaurantOrderStatus.Settled or RestaurantOrderStatus.Cancelled)
            throw new InvalidOperationException("سفارش بسته‌شده قابل ویرایش نیست.");
    }

    private void Recalculate()
    {
        SubTotal = Items.Where(i => i.Status != OrderItemStatus.Cancelled).Sum(i => i.LineTotal);
        GrandTotal = SubTotal - Discount + ServiceCharge + Tax + Tip;
    }
}
