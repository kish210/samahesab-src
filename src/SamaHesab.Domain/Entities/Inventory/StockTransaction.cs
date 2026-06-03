namespace SamaHesab.Domain.Entities.Inventory;

/// <summary>
/// Immutable inventory ledger entry (kardex). Every stock movement — purchase,
/// sale, transfer, adjustment — writes one row so the running balance per
/// product/warehouse can be reconstructed.
/// Maps to Inv.StockTransactions (BIGINT identity key).
/// </summary>
public class StockTransaction
{
    public long Id { get; private set; }
    public int CompanyId { get; private set; }
    public int? BranchId { get; private set; }
    public string TransactionType { get; private set; } = default!;   // ورود / خروج / انتقال
    public string? DocumentNumber { get; private set; }
    public string DocumentDate { get; private set; } = default!;       // Jalali yyyy/MM/dd
    public int ProductId { get; private set; }
    public int WarehouseId { get; private set; }
    public int? BatchId { get; private set; }
    public int? SerialId { get; private set; }
    public decimal Quantity { get; private set; }                      // +in / -out
    public decimal UnitCost { get; private set; }
    public decimal TotalCost { get; private set; }
    public decimal BalanceQty { get; private set; }
    public decimal BalanceCost { get; private set; }
    public string? RelatedDocType { get; private set; }
    public int? RelatedDocId { get; private set; }
    public string? Notes { get; private set; }
    public int? CreatedByUserId { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.Now;

    private StockTransaction() { }

    public static StockTransaction Create(
        int companyId, int? branchId, string transactionType, string? documentNumber,
        string documentDate, int productId, int warehouseId,
        decimal quantity, decimal unitCost, decimal balanceQty, decimal balanceCost,
        string? relatedDocType = null, int? relatedDocId = null, string? notes = null)
    {
        return new StockTransaction
        {
            CompanyId = companyId,
            BranchId = branchId,
            TransactionType = transactionType,
            DocumentNumber = documentNumber,
            DocumentDate = documentDate,
            ProductId = productId,
            WarehouseId = warehouseId,
            Quantity = quantity,
            UnitCost = unitCost,
            TotalCost = quantity * unitCost,
            BalanceQty = balanceQty,
            BalanceCost = balanceCost,
            RelatedDocType = relatedDocType,
            RelatedDocId = relatedDocId,
            Notes = notes
        };
    }
}
