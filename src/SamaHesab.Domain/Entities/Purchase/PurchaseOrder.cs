using SamaHesab.Domain.Common;

namespace SamaHesab.Domain.Entities.Purchase;

/// <summary>
/// سفارش خرید (Purchase Order) — درخواست تأمین کالا، پیش از فاکتور خرید.
/// می‌تواند دستی یا به‌صورت خودکار از «پیشنهاد سفارش» (نقطه‌ی سفارش) ساخته شود.
/// </summary>
public class PurchaseOrder : AuditableEntity
{
    public int BranchId { get; private set; }
    public string OrderNumber { get; private set; } = default!;
    public string OrderDate { get; private set; } = default!;     // شمسی yyyy/MM/dd
    public int? SupplierId { get; private set; }
    public string StatusCode { get; private set; } = "پیش‌نویس";   // پیش‌نویس / تأییدشده / بسته / لغو
    public string Source { get; private set; } = "دستی";          // دستی / خودکار
    public string? Description { get; private set; }
    public decimal Total { get; private set; }

    public ICollection<PurchaseOrderItem> Items { get; private set; } = new List<PurchaseOrderItem>();

    private PurchaseOrder() { }

    public static PurchaseOrder Create(int companyId, int branchId, string orderNumber,
        string orderDate, int? supplierId = null, string source = "دستی", string? description = null)
    {
        if (string.IsNullOrWhiteSpace(orderNumber))
            throw new ArgumentException("شماره سفارش الزامی است.");
        return new PurchaseOrder
        {
            CompanyId = companyId,
            BranchId = branchId,
            OrderNumber = orderNumber,
            OrderDate = orderDate,
            SupplierId = supplierId,
            Source = source,
            Description = description
        };
    }

    public void AddItem(int productId, decimal quantity, decimal unitPrice)
    {
        if (quantity <= 0) throw new ArgumentException("تعداد باید بزرگتر از صفر باشد.");
        Items.Add(PurchaseOrderItem.Create(0, Items.Count + 1, productId, quantity, unitPrice));
        Recalculate();
    }

    private void Recalculate() => Total = Items.Sum(i => i.LineTotal);

    public void Approve()
    {
        if (StatusCode != "پیش‌نویس") throw new InvalidOperationException("فقط سفارش پیش‌نویس قابل تأیید است.");
        if (!Items.Any()) throw new InvalidOperationException("سفارش بدون ردیف قابل تأیید نیست.");
        StatusCode = "تأییدشده";
        UpdatedAt = DateTime.Now;
    }

    public void Cancel()
    {
        if (StatusCode == "بسته") throw new InvalidOperationException("سفارش بسته قابل لغو نیست.");
        StatusCode = "لغو";
        UpdatedAt = DateTime.Now;
    }

    public void Close()
    {
        StatusCode = "بسته";
        UpdatedAt = DateTime.Now;
    }
}
