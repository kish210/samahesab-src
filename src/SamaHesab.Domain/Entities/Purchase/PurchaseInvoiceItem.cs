using SamaHesab.Domain.Common;

namespace SamaHesab.Domain.Entities.Purchase;

public class PurchaseInvoiceItem : BaseEntity
{
    public int InvoiceId { get; private set; }
    public int RowNumber { get; private set; }
    public int ProductId { get; private set; }
    public int? BatchId { get; private set; }
    public int? SerialId { get; private set; }
    public string? Description { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal DiscountPct { get; private set; }
    public decimal DiscountAmount { get; private set; }
    public decimal TaxPct { get; private set; }
    public decimal TaxAmount { get; private set; }
    public decimal NetAmount { get; private set; }
    public decimal AdditionalCost { get; private set; }
    public decimal LandedCost { get; private set; }

    private PurchaseInvoiceItem() { }

    public static PurchaseInvoiceItem Create(int invoiceId, int rowNumber, int productId,
        decimal quantity, decimal unitPrice, decimal discountPct = 0, decimal taxPct = 0,
        string? description = null, int? batchId = null, int? serialId = null)
    {
        if (quantity <= 0) throw new ArgumentException("مقدار باید بزرگتر از صفر باشد.");
        if (unitPrice < 0) throw new ArgumentException("قیمت واحد نمی‌تواند منفی باشد.");

        var item = new PurchaseInvoiceItem
        {
            InvoiceId = invoiceId,
            RowNumber = rowNumber,
            ProductId = productId,
            Quantity = quantity,
            UnitPrice = unitPrice,
            DiscountPct = discountPct,
            TaxPct = taxPct,
            Description = description,
            BatchId = batchId,
            SerialId = serialId
        };
        item.Calculate();
        return item;
    }

    /// <summary>U-ACCT-1.5 — سهمِ این ردیف از حمل/سایرهزینه‌هایِ سرفاکتور (توزیع‌شده به‌نسبتِ
    /// NetAmount توسطِ فراخواننده). LandedCost را بازمحاسبه می‌کند.</summary>
    public void SetAdditionalCost(decimal amount)
    {
        if (amount < 0) throw new ArgumentException("هزینهٔ اضافی نمی‌تواند منفی باشد.");
        AdditionalCost = amount;
        Calculate();
    }

    private void Calculate()
    {
        var subtotal = Quantity * UnitPrice;
        DiscountAmount = subtotal * DiscountPct / 100;
        var afterDiscount = subtotal - DiscountAmount;
        TaxAmount = afterDiscount * TaxPct / 100;
        NetAmount = afterDiscount + TaxAmount;
        // U-ACCT-1.1/1.5: LandedCost عمداً TaxAmount را ندارد — از رفعِ U-ACCT-1.1، مالیاتِ خرید
        // دیگر داخلِ ارزشِ موجودی folded نمی‌شود (حسابِ جداگانهٔ ۱-۰۶-۰۰۱، مالیاتِ قابلِ‌کسر). پس
        // LandedCost یعنی بهایِ واقعاً قابلِ‌سرمایه‌گذاری در موجودی: خالص‌ازتخفیف + سهمِ حمل/سایرهزینه‌ها.
        LandedCost = afterDiscount + AdditionalCost;
    }
}
