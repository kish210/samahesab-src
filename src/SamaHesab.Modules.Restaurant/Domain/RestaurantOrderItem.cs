using SamaHesab.Domain.Common;
using SamaHesab.Domain.Enums;

namespace SamaHesab.Modules.Restaurant.Domain;

/// <summary>یک ردیف از سفارش رستوران (یک غذا/نوشیدنی). وضعیت هر ردیف جدا دنبال می‌شود
/// تا آشپزخانه و گارسون بدانند کدام آیتم آماده/سرو شده است.</summary>
public class RestaurantOrderItem : AuditableEntity
{
    public int OrderId { get; private set; }
    public int ProductId { get; private set; }
    public string ProductName { get; private set; } = default!;
    public decimal Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal DiscountAmount { get; private set; }
    public decimal LineTotal { get; private set; }
    public OrderItemStatus Status { get; private set; } = OrderItemStatus.Pending;
    public string? Notes { get; private set; }          // یادداشت آشپزخانه، مثل «بدون پیاز»
    public int? KitchenTicketId { get; private set; }

    private RestaurantOrderItem() { }

    public static RestaurantOrderItem Create(int companyId, int productId, string productName,
        decimal quantity, decimal unitPrice, decimal discountAmount = 0, string? notes = null)
    {
        if (quantity <= 0) throw new ArgumentException("تعداد باید بزرگ‌تر از صفر باشد.");
        var item = new RestaurantOrderItem
        {
            CompanyId = companyId,
            ProductId = productId,
            ProductName = productName,
            Quantity = quantity,
            UnitPrice = unitPrice,
            DiscountAmount = discountAmount,
            Notes = notes
        };
        item.Recalculate();
        return item;
    }

    public void ChangeQuantity(decimal quantity)
    {
        if (Status != OrderItemStatus.Pending)
            throw new InvalidOperationException("فقط ردیف ارسال‌نشده به آشپزخانه قابل ویرایش است.");
        if (quantity <= 0) throw new ArgumentException("تعداد باید بزرگ‌تر از صفر باشد.");
        Quantity = quantity;
        Recalculate();
    }

    public void SetNotes(string? notes) { Notes = notes; UpdatedAt = DateTime.Now; }

    internal void AttachToKitchenTicket(int ticketId)
    {
        KitchenTicketId = ticketId;
        Status = OrderItemStatus.InKitchen;
        UpdatedAt = DateTime.Now;
    }

    public void MarkPreparing() { Status = OrderItemStatus.Preparing; UpdatedAt = DateTime.Now; }
    public void MarkReady() { Status = OrderItemStatus.Ready; UpdatedAt = DateTime.Now; }
    public void MarkServed() { Status = OrderItemStatus.Served; UpdatedAt = DateTime.Now; }
    public void Cancel() { Status = OrderItemStatus.Cancelled; Recalculate(); }

    private void Recalculate() =>
        LineTotal = Status == OrderItemStatus.Cancelled ? 0 : (Quantity * UnitPrice) - DiscountAmount;
}
