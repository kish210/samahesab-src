using SamaHesab.Domain.Common;

namespace SamaHesab.Modules.Tourism.Domain;

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
    /// <summary>ظرفیتِ کلِ محصول (تعدادِ قابلِ‌فروش). null = نامحدود — فروشنده ماندهٔ ظرفیت را می‌بیند.</summary>
    public int? Capacity { get; private set; }
    public bool Active { get; private set; } = true;

    /// <summary>مبنای پورسانتِ بازاریاب (مبلغِ ثابت/درصدِ فروش/درصدِ سود).</summary>
    public CommissionBasis MarketerCommissionBasis { get; private set; } = CommissionBasis.PercentOfProfit;
    /// <summary>مقدارِ پورسانت (مبلغ یا درصد).</summary>
    public decimal MarketerCommissionValue { get; private set; }

    /// <summary>سودِ خالصِ هر واحد (فروش − خرید) — محاسبه‌شده، در EF مپ نمی‌شود.</summary>
    public decimal NetProfit => DefaultSalePrice - PurchasePrice;

    /// <summary>مبلغِ پورسانتِ بازاریاب به‌ازای هر واحد — محاسبه‌شده، در EF مپ نمی‌شود.</summary>
    public decimal MarketerCommission => MarketerCommissionBasis switch
    {
        CommissionBasis.PerUnit         => MarketerCommissionValue,
        CommissionBasis.PercentOfSale   => DefaultSalePrice * MarketerCommissionValue / 100m,
        CommissionBasis.PercentOfProfit => NetProfit * MarketerCommissionValue / 100m,
        _                               => 0m
    };

    private TourismProduct() { }

    public static TourismProduct Create(int companyId, string name, int supplierPartyId,
        decimal purchasePrice, decimal defaultSalePrice, int? productGroupId = null,
        bool requiresPassengerList = false, int? capacity = null,
        CommissionBasis commissionBasis = CommissionBasis.PercentOfProfit, decimal commissionValue = 0)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("نامِ محصول الزامی است.");
        if (supplierPartyId <= 0) throw new ArgumentException("تأمین‌کننده الزامی است.");
        if (purchasePrice < 0 || defaultSalePrice < 0) throw new ArgumentException("قیمت نمی‌تواند منفی باشد.");
        if (capacity is < 0) throw new ArgumentException("ظرفیت نمی‌تواند منفی باشد.");
        return new TourismProduct
        {
            CompanyId = companyId, Name = name, SupplierPartyId = supplierPartyId,
            PurchasePrice = purchasePrice, DefaultSalePrice = defaultSalePrice,
            ProductGroupId = productGroupId, RequiresPassengerList = requiresPassengerList,
            Capacity = capacity,
            MarketerCommissionBasis = commissionBasis, MarketerCommissionValue = commissionValue < 0 ? 0 : commissionValue
        };
    }

    public void Update(string name, int supplierPartyId, decimal purchasePrice, decimal defaultSalePrice,
        int? productGroupId, bool requiresPassengerList, bool active, int? capacity = null,
        CommissionBasis commissionBasis = CommissionBasis.PercentOfProfit, decimal commissionValue = 0)
    {
        if (!string.IsNullOrWhiteSpace(name)) Name = name;
        if (supplierPartyId > 0) SupplierPartyId = supplierPartyId;
        PurchasePrice = purchasePrice < 0 ? 0 : purchasePrice;
        DefaultSalePrice = defaultSalePrice < 0 ? 0 : defaultSalePrice;
        ProductGroupId = productGroupId;
        RequiresPassengerList = requiresPassengerList;
        Capacity = capacity is < 0 ? 0 : capacity;
        MarketerCommissionBasis = commissionBasis;
        MarketerCommissionValue = commissionValue < 0 ? 0 : commissionValue;
        Active = active;
        SetAudit(null);
    }
}
