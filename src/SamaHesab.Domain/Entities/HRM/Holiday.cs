using SamaHesab.Domain.Common;

namespace SamaHesab.Domain.Entities.HRM;

/// <summary>
/// ATT-C1-1 — روزِ تعطیلِ تقویمِ کاری (تعطیلاتِ رسمی + تعطیلیِ سازمانی).
/// جمعه به‌صورتِ ضمنی تعطیل است؛ این جدول روزهای تعطیلِ غیرجمعه را نگه می‌دارد.
/// موتورِ تردد از آن برای تشخیصِ «جمعه/تعطیل‌کاری» و روزِ کاریِ مؤظف استفاده می‌کند.
/// </summary>
public class Holiday : BaseEntity
{
    public int CompanyId { get; private set; }
    public string Date { get; private set; } = default!;   // تاریخِ شمسی «۱۴۰۴/۰۱/۰۱»
    public string Title { get; private set; } = default!;  // مناسبت
    public bool IsOfficial { get; private set; } = true;   // رسمی (تعطیلِ باحقوق) یا سازمانی

    private Holiday() { }

    public static Holiday Create(int companyId, string date, string title, bool isOfficial = true)
    {
        if (string.IsNullOrWhiteSpace(date)) throw new ArgumentException("تاریخِ تعطیلی الزامی است.");
        return new Holiday
        {
            CompanyId = companyId, Date = date,
            Title = string.IsNullOrWhiteSpace(title) ? "تعطیل" : title, IsOfficial = isOfficial
        };
    }

    public void Update(string title, bool isOfficial)
    {
        if (!string.IsNullOrWhiteSpace(title)) Title = title;
        IsOfficial = isOfficial;
    }
}
