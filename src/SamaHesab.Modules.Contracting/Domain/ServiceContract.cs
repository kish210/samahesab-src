using SamaHesab.Domain.Common;

namespace SamaHesab.Modules.Contracting.Domain;

/// <summary>
/// قراردادِ خدماتیِ تکرارشونده (اینترنت/پشتیبانی/...) — جایگزینِ فیچرِ قدیمیِ «فاکتورهایِ تکرارشونده»
/// که برایِ هر کالا/انبار کار می‌کرد. اینجا فقط برایِ خدمات و در قالبِ یک قرارداد است.
/// موتور در سررسید یک پیش‌فاکتور (Draft) تولید می‌کند؛ آیتم‌هایِ اضافیِ همان‌دوره (اگر کالایی هم
/// در آن ماه فروخته شده باشد) از <see cref="ServiceContractExtraItem"/> به همان فاکتور اضافه می‌شوند.
/// </summary>
public class ServiceContract : AuditableEntity
{
    public int BranchId { get; private set; }
    public string Title { get; private set; } = default!;
    public int CustomerId { get; private set; }
    public int WarehouseId { get; private set; }
    public string PriceLevel { get; private set; } = "خرده";

    /// <summary>محصولِ خدماتیِ تکرارشونده (باید از نوعِ «خدمت» باشد).</summary>
    public int ServiceProductId { get; private set; }
    public decimal MonthlyAmount { get; private set; }
    public decimal TaxPct { get; private set; }

    public int Frequency { get; private set; }                 // هم‌راستا با RecurrenceFrequency
    public string NextInvoiceDate { get; private set; } = default!;   // «YYYY/MM/DD» شمسی
    public string? LastGeneratedDate { get; private set; }
    public bool IsActive { get; private set; } = true;
    public string? Description { get; private set; }

    public ICollection<ServiceContractExtraItem> ExtraItems { get; private set; } = new List<ServiceContractExtraItem>();

    private ServiceContract() { }

    public static ServiceContract Create(int companyId, int branchId, string title, int customerId,
        int warehouseId, int serviceProductId, decimal monthlyAmount, int frequency, string nextInvoiceDate,
        decimal taxPct = 0, string priceLevel = "خرده", string? description = null)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("عنوانِ قرارداد الزامی است.");
        if (customerId <= 0) throw new ArgumentException("مشتری الزامی است.");
        if (serviceProductId <= 0) throw new ArgumentException("خدمتِ قرارداد الزامی است.");
        if (monthlyAmount <= 0) throw new ArgumentException("مبلغِ ماهانه باید بزرگ‌تر از صفر باشد.");
        if (string.IsNullOrWhiteSpace(nextInvoiceDate)) throw new ArgumentException("تاریخِ سررسید الزامی است.");
        return new ServiceContract
        {
            CompanyId = companyId,
            BranchId = branchId,
            Title = title,
            CustomerId = customerId,
            WarehouseId = warehouseId,
            ServiceProductId = serviceProductId,
            MonthlyAmount = monthlyAmount,
            TaxPct = taxPct,
            Frequency = frequency,
            NextInvoiceDate = nextInvoiceDate,
            PriceLevel = priceLevel,
            Description = description
        };
    }

    /// <summary>پس از تولیدِ یک نمونه، سررسید بعدی و تاریخ آخرین تولید ثبت می‌شود.</summary>
    public void MarkGenerated(string generatedDate, string nextDate)
    {
        LastGeneratedDate = generatedDate;
        NextInvoiceDate = nextDate;
        UpdatedAt = DateTime.Now;
    }

    /// <summary>فسخ/عدمِ تمدیدِ قرارداد — دیگر فاکتورِ جدیدی برایش تولید نمی‌شود.</summary>
    public void Terminate() { IsActive = false; UpdatedAt = DateTime.Now; }
}
