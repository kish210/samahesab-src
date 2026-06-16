using SamaHesab.Domain.Common;

namespace SamaHesab.Domain.Entities.Purchase;

public class PurchaseInvoice : AuditableEntity, IBranchScoped   // MB-2: جداسازیِ شعبه
{
    public int BranchId { get; private set; }
    public int FiscalYearId { get; private set; }
    public string InvoiceNumber { get; private set; } = default!;
    public string InvoiceDate { get; private set; } = default!;
    public string InvoiceType { get; private set; } = "خرید";
    public int SupplierId { get; private set; }
    public int WarehouseId { get; private set; }
    public int? OrderId { get; private set; }
    public decimal SubTotal { get; private set; }
    public decimal TotalDiscount { get; private set; }
    public decimal TotalTax { get; private set; }
    public decimal Shipping { get; private set; }
    public decimal OtherCosts { get; private set; }
    public decimal GrandTotal { get; private set; }
    public decimal PaidAmount { get; private set; }
    public decimal RemainAmount { get; private set; }
    public string StatusCode { get; private set; } = "پیش‌نویس";
    public string? DueDate { get; private set; }
    public string? Description { get; private set; }
    public int? VoucherId { get; private set; }
    public int? ReturnedFromId { get; private set; }

    public ICollection<PurchaseInvoiceItem> Items { get; private set; } = new List<PurchaseInvoiceItem>();

    private PurchaseInvoice() { }

    public static PurchaseInvoice Create(int companyId, int branchId, int fiscalYearId,
        string invoiceNumber, string invoiceDate, int supplierId, int warehouseId,
        string invoiceType = "خرید", int? orderId = null,
        string? dueDate = null, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(invoiceNumber))
            throw new ArgumentException("شماره فاکتور الزامی است.");

        return new PurchaseInvoice
        {
            CompanyId = companyId,
            BranchId = branchId,
            FiscalYearId = fiscalYearId,
            InvoiceNumber = invoiceNumber,
            InvoiceDate = invoiceDate,
            SupplierId = supplierId,
            WarehouseId = warehouseId,
            InvoiceType = invoiceType,
            OrderId = orderId,
            DueDate = dueDate,
            Description = description
        };
    }

    public void AddItem(PurchaseInvoiceItem item)
    {
        Items.Add(item);
        RecalculateTotals();
    }

    public void SetShipping(decimal shipping, decimal otherCosts)
    {
        Shipping = shipping;
        OtherCosts = otherCosts;
        RecalculateTotals();
    }

    public void SetPaid(decimal paid)
    {
        PaidAmount = paid;
        RemainAmount = GrandTotal - PaidAmount;
    }

    private void RecalculateTotals()
    {
        SubTotal = Items.Sum(i => i.Quantity * i.UnitPrice);
        TotalDiscount = Items.Sum(i => i.DiscountAmount);
        TotalTax = Items.Sum(i => i.TaxAmount);
        GrandTotal = SubTotal - TotalDiscount + TotalTax + Shipping + OtherCosts;
        RemainAmount = GrandTotal - PaidAmount;
    }

    public void Post(int voucherId)
    {
        StatusCode = "قطعی";
        VoucherId = voucherId;
        UpdatedAt = DateTime.Now;
    }

    public void SetVoucher(int voucherId) { VoucherId = voucherId; UpdatedAt = DateTime.Now; }

    public void SetReturnedFrom(int originalInvoiceId) { ReturnedFromId = originalInvoiceId; UpdatedAt = DateTime.Now; }
}
