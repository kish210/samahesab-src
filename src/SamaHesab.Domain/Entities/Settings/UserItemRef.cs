using SamaHesab.Domain.Common;

namespace SamaHesab.Domain.Entities.Settings;

/// <summary>
/// ارجاع کاربر به یک آیتم پرکاربرد (مشتری/کالا/حساب/سند …) برای بهره‌وری:
/// «اخیر» (بر اساس LastUsedAt) و «سنجاق‌شده» (Pinned). هر کاربر فهرست خودش را دارد.
/// EntityType رشته‌ی نوع است: "Customer" / "Product" / "Account" / "Voucher" …
/// </summary>
public class UserItemRef : AuditableEntity
{
    public int UserId { get; private set; }
    public string EntityType { get; private set; } = default!;
    public int EntityId { get; private set; }
    public string Label { get; private set; } = default!;   // عنوان نمایشی (snapshot)
    public bool Pinned { get; private set; }
    public DateTime LastUsedAt { get; private set; } = DateTime.Now;
    public int UseCount { get; private set; }

    private UserItemRef() { }

    public static UserItemRef Create(int companyId, int userId, string entityType, int entityId, string label)
    {
        if (string.IsNullOrWhiteSpace(entityType)) throw new ArgumentException("نوع آیتم الزامی است.");
        if (entityId <= 0) throw new ArgumentException("شناسه‌ی آیتم نامعتبر است.");
        return new UserItemRef
        {
            CompanyId = companyId,
            UserId = userId,
            EntityType = entityType.Trim(),
            EntityId = entityId,
            Label = label ?? string.Empty,
            LastUsedAt = DateTime.Now,
            UseCount = 1
        };
    }

    /// <summary>ثبت یک استفاده‌ی تازه (برای فهرست «اخیر»).</summary>
    public void Touch(string? label = null)
    {
        UseCount++;
        LastUsedAt = DateTime.Now;
        if (!string.IsNullOrWhiteSpace(label)) Label = label!;
        UpdatedAt = DateTime.Now;
    }

    public void SetPinned(bool pinned, string? label = null)
    {
        Pinned = pinned;
        if (!string.IsNullOrWhiteSpace(label)) Label = label!;
        UpdatedAt = DateTime.Now;
    }
}
