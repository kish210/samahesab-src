using SamaHesab.Domain.Common;

namespace SamaHesab.Domain.Entities.Sales;

/// <summary>ردیف فاکتور تکرارشونده (الگوی کالا).</summary>
public class RecurringInvoiceLine : BaseEntity
{
    public int RecurringInvoiceId { get; private set; }
    public int ProductId { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal TaxPct { get; private set; }

    private RecurringInvoiceLine() { }

    public static RecurringInvoiceLine Create(int recurringInvoiceId, int productId,
        decimal quantity, decimal unitPrice, decimal taxPct)
        => new()
        {
            RecurringInvoiceId = recurringInvoiceId,
            ProductId = productId,
            Quantity = quantity,
            UnitPrice = unitPrice,
            TaxPct = taxPct
        };
}
