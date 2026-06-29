using SamaHesab.Domain.Common;

namespace SamaHesab.Modules.TourismItinerary.Domain;

/// <summary>
/// محصول/خدمتِ گردشگری برای برنامه‌ریزیِ اقامتی (تور، بازدید، فعالیت).
/// قیمتِ فروش/هزینه/ظرفیت دارد؛ سودِ خالص محاسبه‌شده است (در EF مپ نمی‌شود).
/// سانس‌های زمانی در <see cref="ProductSession"/> نگه‌داری می‌شوند.
/// </summary>
public class ItineraryProduct : AuditableEntity
{
    public string Name { get; private set; } = default!;
    public int? SupplierPartyId { get; private set; }
    public decimal SalePrice { get; private set; }
    public decimal Cost { get; private set; }
    public int Capacity { get; private set; }
    public bool Active { get; private set; } = true;

    /// <summary>سودِ خالصِ هر واحد (محاسبه‌شده — در EF Ignore می‌شود).</summary>
    public decimal NetProfit => SalePrice - Cost;

    private ItineraryProduct() { }

    public static ItineraryProduct Create(int companyId, string name, decimal salePrice, decimal cost,
        int capacity, int? supplierPartyId = null)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("نامِ محصول الزامی است.");
        if (salePrice < 0) throw new ArgumentException("قیمتِ فروش نمی‌تواند منفی باشد.");
        if (cost < 0) throw new ArgumentException("هزینه نمی‌تواند منفی باشد.");
        if (capacity < 0) throw new ArgumentException("ظرفیت نمی‌تواند منفی باشد.");
        return new ItineraryProduct
        {
            CompanyId = companyId, Name = name.Trim(), SalePrice = salePrice, Cost = cost,
            Capacity = capacity, SupplierPartyId = supplierPartyId
        };
    }

    public void Update(string name, decimal salePrice, decimal cost, int capacity,
        int? supplierPartyId, bool active, int? userId = null)
    {
        if (!string.IsNullOrWhiteSpace(name)) Name = name.Trim();
        SalePrice = salePrice < 0 ? 0 : salePrice;
        Cost = cost < 0 ? 0 : cost;
        Capacity = capacity < 0 ? 0 : capacity;
        SupplierPartyId = supplierPartyId;
        Active = active;
        SetAudit(userId);
    }
}
