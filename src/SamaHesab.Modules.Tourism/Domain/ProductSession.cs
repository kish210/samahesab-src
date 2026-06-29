using SamaHesab.Domain.Common;

namespace SamaHesab.Modules.Tourism.Domain;

/// <summary>
/// سانسِ زمانیِ یک محصولِ گردشگری (مثلِ «صبح ۹–۱۲»). زمان به‌صورتِ دقیقه از نیمه‌شب ذخیره می‌شود
/// تا مقایسهٔ تداخل ساده و قطعی باشد. هر محصول می‌تواند چند سانس داشته باشد.
/// </summary>
public class ProductSession : AuditableEntity
{
    public int ProductId { get; private set; }
    public string Label { get; private set; } = default!;   // برچسبِ نمایشی (مثلِ «صبح»)
    public int StartMinute { get; private set; }            // دقیقه از نیمه‌شب (۰..۱۴۴۰)
    public int EndMinute { get; private set; }
    public int Capacity { get; private set; }               // ظرفیتِ همین سانس
    public bool Active { get; private set; } = true;

    private ProductSession() { }

    public static ProductSession Create(int companyId, int productId, string label,
        int startMinute, int endMinute, int capacity)
    {
        if (productId <= 0) throw new ArgumentException("محصول الزامی است.");
        if (string.IsNullOrWhiteSpace(label)) throw new ArgumentException("برچسبِ سانس الزامی است.");
        if (startMinute is < 0 or > 1440 || endMinute is < 0 or > 1440)
            throw new ArgumentException("زمانِ سانس باید بین ۰ تا ۱۴۴۰ دقیقه باشد.");
        if (endMinute <= startMinute) throw new ArgumentException("پایانِ سانس باید پس از شروع باشد.");
        if (capacity < 0) throw new ArgumentException("ظرفیت نمی‌تواند منفی باشد.");
        return new ProductSession
        {
            CompanyId = companyId, ProductId = productId, Label = label.Trim(),
            StartMinute = startMinute, EndMinute = endMinute, Capacity = capacity
        };
    }

    public void Update(string label, int startMinute, int endMinute, int capacity, bool active, int? userId = null)
    {
        if (endMinute <= startMinute) throw new ArgumentException("پایانِ سانس باید پس از شروع باشد.");
        if (!string.IsNullOrWhiteSpace(label)) Label = label.Trim();
        StartMinute = startMinute; EndMinute = endMinute;
        Capacity = capacity < 0 ? 0 : capacity; Active = active;
        SetAudit(userId);
    }

    /// <summary>آیا این سانس با سانسِ دیگری در همان روز تداخلِ زمانی دارد؟</summary>
    public bool OverlapsWith(int otherStart, int otherEnd)
        => StartMinute < otherEnd && otherStart < EndMinute;
}
