using SamaHesab.Domain.Common;

namespace SamaHesab.Modules.CRM.Domain;

/// <summary>
/// تراکنشِ امتیازِ باشگاهِ مشتریان (ماژولِ CRM، فاز ۳ استخراج). نگاشت به جدولِ موجودِ
/// `Crm.LoyaltyTransactions`. امتیاز مثبت = کسب، منفی = استفاده؛ موجودی = جمعِ امتیازها.
/// </summary>
public class LoyaltyTransaction : BaseEntity
{
    public int CustomerId { get; private set; }
    public int Points { get; private set; }            // + کسب / − استفاده
    public string Type { get; private set; } = default!; // «کسب» / «استفاده»
    public string? Description { get; private set; }
    public string? RelatedDoc { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.Now;

    private LoyaltyTransaction() { }

    public static LoyaltyTransaction Earn(int customerId, int points, string? description = null, string? relatedDoc = null)
    {
        if (points <= 0) throw new ArgumentException("امتیاز کسب‌شده باید مثبت باشد.");
        return new LoyaltyTransaction { CustomerId = customerId, Points = points, Type = "کسب", Description = description, RelatedDoc = relatedDoc };
    }

    public static LoyaltyTransaction Redeem(int customerId, int points, string? description = null, string? relatedDoc = null)
    {
        if (points <= 0) throw new ArgumentException("امتیاز استفاده‌شده باید مثبت باشد.");
        return new LoyaltyTransaction { CustomerId = customerId, Points = -points, Type = "استفاده", Description = description, RelatedDoc = relatedDoc };
    }
}
