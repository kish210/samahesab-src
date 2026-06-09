using SamaHesab.Domain.Common;

namespace SamaHesab.Domain.Events;

/// <summary>یک سفارش (یا بخشی از آن) به آشپزخانه ارسال شد → ساخت/به‌روزرسانی رسید آشپزخانه.</summary>
public sealed class OrderSentToKitchenEvent : DomainEvent
{
    public int OrderId { get; }
    public int BranchId { get; }
    public int KitchenTicketId { get; }

    public OrderSentToKitchenEvent(int orderId, int branchId, int kitchenTicketId)
    {
        OrderId = orderId;
        BranchId = branchId;
        KitchenTicketId = kitchenTicketId;
    }
}

/// <summary>سفارش رستوران تسویه شد → ثبت فروش/سند (در فازهای بعد).</summary>
public sealed class RestaurantOrderSettledEvent : DomainEvent
{
    public int OrderId { get; }
    public int CompanyId { get; }
    public int BranchId { get; }
    public decimal GrandTotal { get; }
    public int UserId { get; }

    public RestaurantOrderSettledEvent(int orderId, int companyId, int branchId, decimal grandTotal, int userId)
    {
        OrderId = orderId;
        CompanyId = companyId;
        BranchId = branchId;
        GrandTotal = grandTotal;
        UserId = userId;
    }
}
