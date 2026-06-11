using SamaHesab.Domain.Common;

namespace SamaHesab.Domain.Entities.Sales;

/// <summary>
/// فاکتور فروشِ تکرارشونده — تعریف یک فاکتور الگو + زمان‌بندی؛ موتور در سررسید،
/// فاکتور فروش واقعی تولید می‌کند. Frequency: 0=ماهانه، 1=سالانه (هم‌راستا با RecurrenceFrequency).
/// </summary>
public class RecurringInvoice : AuditableEntity
{
    public int BranchId { get; private set; }
    public string Name { get; private set; } = default!;
    public int CustomerId { get; private set; }
    public int WarehouseId { get; private set; }
    public string PriceLevel { get; private set; } = "خرده";
    public int Frequency { get; private set; }                 // 0=ماهانه 1=سالانه
    public string NextDate { get; private set; } = default!;    // «YYYY/MM/DD» شمسی
    public string? LastGeneratedDate { get; private set; }
    public bool IsActive { get; private set; } = true;
    public string? Description { get; private set; }

    public ICollection<RecurringInvoiceLine> Lines { get; private set; } = new List<RecurringInvoiceLine>();

    private RecurringInvoice() { }

    public static RecurringInvoice Create(int companyId, int branchId, string name,
        int customerId, int warehouseId, int frequency, string nextDate,
        string priceLevel = "خرده", string? description = null)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("نام الزامی است.");
        if (string.IsNullOrWhiteSpace(nextDate)) throw new ArgumentException("تاریخ سررسید الزامی است.");
        return new RecurringInvoice
        {
            CompanyId = companyId,
            BranchId = branchId,
            Name = name,
            CustomerId = customerId,
            WarehouseId = warehouseId,
            Frequency = frequency,
            NextDate = nextDate,
            PriceLevel = priceLevel,
            Description = description
        };
    }

    public void AddLine(int productId, decimal quantity, decimal unitPrice, decimal taxPct = 0)
    {
        if (quantity <= 0) throw new ArgumentException("تعداد باید بزرگتر از صفر باشد.");
        Lines.Add(RecurringInvoiceLine.Create(0, productId, quantity, unitPrice, taxPct));
    }

    /// <summary>پس از تولیدِ یک نمونه، سررسید بعدی و تاریخ آخرین تولید ثبت می‌شود.</summary>
    public void MarkGenerated(string generatedDate, string nextDate)
    {
        LastGeneratedDate = generatedDate;
        NextDate = nextDate;
        UpdatedAt = DateTime.Now;
    }

    public void Deactivate() { IsActive = false; UpdatedAt = DateTime.Now; }
}
