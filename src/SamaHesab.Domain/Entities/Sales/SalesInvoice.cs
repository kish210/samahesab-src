using SamaHesab.Domain.Common;
using SamaHesab.Domain.Enums;
using SamaHesab.Domain.Events;

namespace SamaHesab.Domain.Entities.Sales;

public class SalesInvoice : AuditableEntity, IBranchScoped   // MB-2: جداسازیِ شعبه
{
    public int BranchId { get; private set; }
    public int FiscalYearId { get; private set; }
    public string InvoiceNumber { get; private set; } = default!;
    public string InvoiceDate { get; private set; } = default!;
    public InvoiceType InvoiceType { get; private set; }
    public int CustomerId { get; private set; }
    public int WarehouseId { get; private set; }
    public string PriceLevel { get; private set; } = "خرده";
    public int? SalesRepId { get; private set; }
    public int? CurrencyId { get; private set; }
    public decimal ExchangeRate { get; private set; } = 1;
    public decimal SubTotal { get; private set; }
    public decimal TotalDiscount { get; private set; }
    public decimal TotalTax { get; private set; }
    public decimal Shipping { get; private set; }
    public decimal OtherCosts { get; private set; }
    public decimal GrandTotal { get; private set; }
    public decimal PaidAmount { get; private set; }
    public decimal RemainAmount { get; private set; }
    public InvoiceStatus Status { get; private set; } = InvoiceStatus.Draft;
    public string? DueDate { get; private set; }
    public string? Description { get; private set; }
    public int PrintCount { get; private set; }
    public int? VoucherId { get; private set; }
    public int? ReturnedFromId { get; private set; }

    public ICollection<SalesInvoiceItem> Items { get; private set; } = new List<SalesInvoiceItem>();
    public ICollection<SalesPayment> Payments { get; private set; } = new List<SalesPayment>();

    private SalesInvoice() { }

    public static SalesInvoice Create(int companyId, int branchId, int fiscalYearId,
        string invoiceNumber, string invoiceDate, int customerId, int warehouseId,
        InvoiceType invoiceType = InvoiceType.Sale, string priceLevel = "خرده",
        int? salesRepId = null, string? dueDate = null, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(invoiceNumber))
            throw new ArgumentException("شماره فاکتور الزامی است.");

        return new SalesInvoice
        {
            CompanyId = companyId,
            BranchId = branchId,
            FiscalYearId = fiscalYearId,
            InvoiceNumber = invoiceNumber,
            InvoiceDate = invoiceDate,
            CustomerId = customerId,
            WarehouseId = warehouseId,
            InvoiceType = invoiceType,
            PriceLevel = priceLevel,
            SalesRepId = salesRepId,
            DueDate = dueDate,
            Description = description
        };
    }

    public void AddItem(SalesInvoiceItem item)
    {
        if (Status != InvoiceStatus.Draft)
            throw new InvalidOperationException("فقط فاکتورهای پیش‌نویس قابل ویرایش هستند.");
        Items.Add(item);
        RecalculateTotals();
    }

    public void RemoveItem(int itemId)
    {
        if (Status != InvoiceStatus.Draft)
            throw new InvalidOperationException("فقط فاکتورهای پیش‌نویس قابل ویرایش هستند.");
        var item = Items.FirstOrDefault(i => i.Id == itemId)
            ?? throw new InvalidOperationException("ردیف یافت نشد.");
        Items.Remove(item);
        RecalculateTotals();
    }

    public void SetShipping(decimal shipping, decimal otherCosts)
    {
        Shipping = shipping;
        OtherCosts = otherCosts;
        RecalculateTotals();
    }

    private void RecalculateTotals()
    {
        SubTotal = Items.Sum(i => i.Quantity * i.UnitPrice);
        TotalDiscount = Items.Sum(i => i.DiscountAmount);
        TotalTax = Items.Sum(i => i.TaxAmount);
        GrandTotal = SubTotal - TotalDiscount + TotalTax + Shipping + OtherCosts;
        RemainAmount = GrandTotal - PaidAmount;
    }

    public void Confirm()
    {
        if (!Items.Any()) throw new InvalidOperationException("فاکتور باید حداقل یک ردیف داشته باشد.");
        if (Status != InvoiceStatus.Draft) throw new InvalidOperationException("وضعیت فاکتور قابل تأیید نیست.");
        Status = InvoiceStatus.Confirmed;
        UpdatedAt = DateTime.Now;
    }

    public void Post(int userId, int voucherId)
    {
        if (Status != InvoiceStatus.Confirmed && Status != InvoiceStatus.Draft)
            throw new InvalidOperationException("وضعیت فاکتور قابل قطعی کردن نیست.");
        Status = InvoiceStatus.Posted;
        VoucherId = voucherId;
        UpdatedAt = DateTime.Now;
        AddDomainEvent(new SalesInvoicePostedEvent(Id, CompanyId, CustomerId, GrandTotal, userId));
    }

    public void AddPayment(decimal amount)
    {
        PaidAmount += amount;
        RemainAmount = GrandTotal - PaidAmount;
        UpdatedAt = DateTime.Now;
    }

    public void Cancel()
    {
        if (Status == InvoiceStatus.Posted)
            throw new InvalidOperationException("فاکتور قطعی شده را نمی‌توان لغو کرد.");
        Status = InvoiceStatus.Cancelled;
        UpdatedAt = DateTime.Now;
    }

    public void IncrementPrintCount() { PrintCount++; UpdatedAt = DateTime.Now; }
    public void SetVoucher(int voucherId) { VoucherId = voucherId; UpdatedAt = DateTime.Now; }
    public void SetReturnedFrom(int originalInvoiceId) { ReturnedFromId = originalInvoiceId; UpdatedAt = DateTime.Now; }
    public bool IsFullyPaid() => RemainAmount <= 0.01m;
    public bool IsPartiallyPaid() => PaidAmount > 0 && RemainAmount > 0.01m;
}
