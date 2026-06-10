using SamaHesab.Domain.Common;

namespace SamaHesab.Domain.Entities.Accounting;

/// <summary>
/// سند تکرارشونده: یک الگوی سند + زمان‌بندی (دوره‌ی تکرار و تاریخ سررسید بعدی).
/// موتور تولید، در سررسید یک سند پیش‌نویس از الگو می‌سازد و سررسید را جلو می‌برد.
/// Frequency به‌صورت int ذخیره می‌شود (0=ماهانه، 1=سالانه) تا دامنه به Application وابسته نشود.
/// </summary>
public class RecurringVoucher : AuditableEntity
{
    public int BranchId { get; private set; }
    public int TemplateId { get; private set; }
    public string Name { get; private set; } = default!;
    public int Frequency { get; private set; }
    public string NextDate { get; private set; } = default!;   // «YYYY/MM/DD» شمسی
    public string? LastGeneratedDate { get; private set; }
    public bool IsActive { get; private set; } = true;

    private RecurringVoucher() { }

    public static RecurringVoucher Create(int companyId, int branchId, int templateId,
        string name, int frequency, string startDate)
    {
        if (templateId <= 0) throw new ArgumentException("الگو الزامی است.");
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("نام الزامی است.");
        if (string.IsNullOrWhiteSpace(startDate)) throw new ArgumentException("تاریخ شروع الزامی است.");
        return new RecurringVoucher
        {
            CompanyId = companyId,
            BranchId = branchId,
            TemplateId = templateId,
            Name = name.Trim(),
            Frequency = frequency,
            NextDate = startDate
        };
    }

    /// <summary>پس از تولید یک سند: ثبت تاریخ تولید و تنظیم سررسید بعدی.</summary>
    public void MarkGenerated(string generatedDate, string nextDate)
    {
        LastGeneratedDate = generatedDate;
        NextDate = nextDate;
        UpdatedAt = DateTime.Now;
    }

    public void SetActive(bool active) { IsActive = active; UpdatedAt = DateTime.Now; }
}
