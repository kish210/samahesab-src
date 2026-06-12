using SamaHesab.Domain.Common;

namespace SamaHesab.Domain.Entities.Accounting;

/// <summary>
/// سال مالی به‌عنوان موجودیتِ درجه‌یک (هستهٔ ERP): بازهٔ تاریخ شمسی + وضعیت باز/بسته.
/// قفل دوره: ثبت/ویرایش سند فقط در سال مالیِ باز و در بازهٔ تاریخِ آن مجاز است.
/// </summary>
public class FiscalYear : AuditableEntity
{
    public string Title { get; private set; } = default!;      // مثل «۱۴۰۴»
    public string StartDate { get; private set; } = default!;   // شمسی yyyy/MM/dd
    public string EndDate { get; private set; } = default!;
    public bool IsClosed { get; private set; }                  // بسته‌شده (سند اختتامیه صادر شده)
    public bool IsActive { get; private set; } = true;          // سال مالی جاری/پیش‌فرض

    private FiscalYear() { }

    public static FiscalYear Create(int companyId, string title, string startDate, string endDate)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("عنوان سال مالی الزامی است.");
        if (string.IsNullOrWhiteSpace(startDate) || string.IsNullOrWhiteSpace(endDate))
            throw new ArgumentException("تاریخ شروع و پایان سال مالی الزامی است.");
        if (string.CompareOrdinal(endDate, startDate) < 0)
            throw new ArgumentException("تاریخ پایان نمی‌تواند پیش از تاریخ شروع باشد.");

        return new FiscalYear { CompanyId = companyId, Title = title, StartDate = startDate, EndDate = endDate };
    }

    public void Update(string title, string startDate, string endDate)
    {
        if (IsClosed) throw new InvalidOperationException("سال مالیِ بسته‌شده قابل ویرایش نیست.");
        Title = title; StartDate = startDate; EndDate = endDate; SetAudit(null);
    }

    public void Close() { IsClosed = true; IsActive = false; SetAudit(null); }
    public void Reopen() { IsClosed = false; SetAudit(null); }
    public void Activate() { IsActive = true; SetAudit(null); }
    public void Deactivate() { IsActive = false; SetAudit(null); }

    /// <summary>آیا تاریخِ داده‌شده در بازهٔ این سال مالی است؟ (مقایسهٔ لغویِ تاریخ شمسی)</summary>
    public bool Contains(string date) =>
        !string.IsNullOrEmpty(date) &&
        string.CompareOrdinal(date, StartDate) >= 0 &&
        string.CompareOrdinal(date, EndDate) <= 0;
}
