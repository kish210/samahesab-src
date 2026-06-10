using SamaHesab.Domain.Common;

namespace SamaHesab.Domain.Entities.Inventory;

/// <summary>
/// سند انبارگردانی: شمارش فیزیکی یک انبار. هنگام شروع، موجودی سیستمی هر کالا snapshot می‌شود؛
/// کاربر تعداد شمرده‌شده را وارد می‌کند؛ هنگام نهایی‌سازی، اختلاف‌ها به تعدیل موجودی تبدیل می‌شوند.
/// </summary>
public class StockCountSession : AuditableEntity
{
    public int BranchId { get; private set; }
    public int WarehouseId { get; private set; }
    public string Date { get; private set; } = default!;   // «YYYY/MM/DD» شمسی
    public int Status { get; private set; }                  // 0=باز 1=نهایی‌شده
    public DateTime? PostedAt { get; private set; }

    public ICollection<StockCountLine> Lines { get; private set; } = new List<StockCountLine>();

    private StockCountSession() { }

    public static StockCountSession Create(int companyId, int branchId, int warehouseId, string date)
    {
        if (warehouseId <= 0) throw new ArgumentException("انبار الزامی است.");
        if (string.IsNullOrWhiteSpace(date)) throw new ArgumentException("تاریخ الزامی است.");
        return new StockCountSession
        {
            CompanyId = companyId, BranchId = branchId, WarehouseId = warehouseId, Date = date
        };
    }

    public void AddLine(StockCountLine line)
    {
        if (Status != 0) throw new InvalidOperationException("سند انبارگردانیِ نهایی‌شده قابل ویرایش نیست.");
        Lines.Add(line);
    }

    public bool IsPosted => Status == 1;

    public void Post()
    {
        if (Status != 0) throw new InvalidOperationException("این سند قبلاً نهایی شده است.");
        Status = 1;
        PostedAt = DateTime.Now;
        UpdatedAt = DateTime.Now;
    }

    /// <summary>ردیف‌هایی که شمارش با سیستم اختلاف دارد (نیازمند تعدیل).</summary>
    public IEnumerable<StockCountLine> VarianceLines() => Lines.Where(l => l.Variance != 0);
}
