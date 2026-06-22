using SamaHesab.Domain.Common;

namespace SamaHesab.Domain.Entities.Tourism;

/// <summary>
/// TUR-C1-1 — خدمت/محصولِ گردشگری. هزینهٔ خرید از ودیعهٔ تأمین‌کننده برداشت می‌شود.
/// RequiresPassengerList: محصولاتی مثلِ تور/گشتِ جزیره که لیستِ مسافر لازم دارند.
/// </summary>
public class TourismProduct : AuditableEntity
{
    public string Name { get; private set; } = default!;
    public int SupplierPartyId { get; private set; }       // تأمین‌کننده (Party)
    public decimal PurchasePrice { get; private set; }     // بهای خرید (برداشت از ودیعه)
    public decimal DefaultSalePrice { get; private set; }
    public int? ProductGroupId { get; private set; }
    public bool RequiresPassengerList { get; private set; }
    public bool Active { get; private set; } = true;

    private TourismProduct() { }

    public static TourismProduct Create(int companyId, string name, int supplierPartyId,
        decimal purchasePrice, decimal defaultSalePrice, int? productGroupId = null,
        bool requiresPassengerList = false)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("نامِ محصول الزامی است.");
        if (supplierPartyId <= 0) throw new ArgumentException("تأمین‌کننده الزامی است.");
        if (purchasePrice < 0 || defaultSalePrice < 0) throw new ArgumentException("قیمت نمی‌تواند منفی باشد.");
        return new TourismProduct
        {
            CompanyId = companyId, Name = name, SupplierPartyId = supplierPartyId,
            PurchasePrice = purchasePrice, DefaultSalePrice = defaultSalePrice,
            ProductGroupId = productGroupId, RequiresPassengerList = requiresPassengerList
        };
    }

    public void Update(string name, int supplierPartyId, decimal purchasePrice, decimal defaultSalePrice,
        int? productGroupId, bool requiresPassengerList, bool active)
    {
        if (!string.IsNullOrWhiteSpace(name)) Name = name;
        if (supplierPartyId > 0) SupplierPartyId = supplierPartyId;
        PurchasePrice = purchasePrice < 0 ? 0 : purchasePrice;
        DefaultSalePrice = defaultSalePrice < 0 ? 0 : defaultSalePrice;
        ProductGroupId = productGroupId;
        RequiresPassengerList = requiresPassengerList;
        Active = active;
        SetAudit(null);
    }
}
