using SamaHesab.Domain.Common;

namespace SamaHesab.Domain.Entities.Purchase;

/// <summary>ردیف سفارش خرید.</summary>
public class PurchaseOrderItem : BaseEntity
{
    public int OrderId { get; private set; }
    public int RowNumber { get; private set; }
    public int ProductId { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal LineTotal { get; private set; }

    private PurchaseOrderItem() { }

    public static PurchaseOrderItem Create(int orderId, int rowNumber, int productId,
        decimal quantity, decimal unitPrice)
        => new()
        {
            OrderId = orderId,
            RowNumber = rowNumber,
            ProductId = productId,
            Quantity = quantity,
            UnitPrice = unitPrice,
            LineTotal = quantity * unitPrice
        };
}
