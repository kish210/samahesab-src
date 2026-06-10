using SamaHesab.Domain.Common;

namespace SamaHesab.Domain.Entities.Inventory;

/// <summary>یک ردیف انبارگردانی: موجودی سیستمی (snapshot) در برابر تعداد شمرده‌شده + مغایرت.</summary>
public class StockCountLine : BaseEntity
{
    public int SessionId { get; private set; }
    public int ProductId { get; private set; }
    public string ProductName { get; private set; } = default!;
    public decimal SystemQty { get; private set; }
    public decimal CountedQty { get; private set; }

    /// <summary>مغایرت = شمارش − سیستم (مثبت: اضافی، منفی: کسری).</summary>
    public decimal Variance => CountedQty - SystemQty;

    private StockCountLine() { }

    public static StockCountLine Create(int sessionId, int productId, string productName, decimal systemQty)
        => new()
        {
            SessionId = sessionId,
            ProductId = productId,
            ProductName = productName ?? string.Empty,
            SystemQty = systemQty,
            CountedQty = systemQty   // پیش‌فرض = سیستم؛ کاربر در صورت اختلاف تغییر می‌دهد
        };

    public void SetCounted(decimal counted)
    {
        if (counted < 0) throw new ArgumentException("تعداد شمرده‌شده نمی‌تواند منفی باشد.");
        CountedQty = counted;
    }
}
