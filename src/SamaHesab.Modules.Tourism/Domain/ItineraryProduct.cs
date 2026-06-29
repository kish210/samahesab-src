using SamaHesab.Domain.Common;

namespace SamaHesab.Modules.Tourism.Domain;

/// <summary>
/// محصول/خدمتِ گردشگری برای برنامه‌ریزیِ اقامتی (تور، بازدید، فعالیت).
/// قیمتِ فروش/هزینه/ظرفیت + تأمین‌کننده + پورسانتِ بازاریاب دارد؛ سود/پورسانت محاسبه‌شده‌اند (در EF مپ نمی‌شوند).
/// سانس‌های زمانی در <see cref="ProductSession"/> نگه‌داری می‌شوند.
/// </summary>
public class ItineraryProduct : AuditableEntity
{
    public string Name { get; private set; } = default!;
    /// <summary>تأمین‌کننده (شخص از اشخاص) که این محصول از او خریداری می‌شود — برای انطباق با حسابداری.</summary>
    public int? SupplierPartyId { get; private set; }
    public decimal SalePrice { get; private set; }
    public decimal Cost { get; private set; }
    public int Capacity { get; private set; }
    public bool Active { get; private set; } = true;

    /// <summary>مبنای پورسانتِ بازاریاب: مبلغِ ثابت (PerUnit) / درصدِ فروش / درصدِ سود.</summary>
    public CommissionBasis MarketerCommissionBasis { get; private set; } = CommissionBasis.PercentOfProfit;
    /// <summary>مقدارِ پورسانت: مبلغ (PerUnit) یا درصد (PercentOfSale/PercentOfProfit).</summary>
    public decimal MarketerCommissionValue { get; private set; }

    /// <summary>سودِ خالصِ هر واحد (محاسبه‌شده — در EF Ignore می‌شود).</summary>
    public decimal NetProfit => SalePrice - Cost;

    /// <summary>مبلغِ پورسانتِ بازاریاب به‌ازای هر واحد (محاسبه‌شده — در EF Ignore می‌شود).</summary>
    public decimal MarketerCommission => MarketerCommissionBasis switch
    {
        CommissionBasis.PerUnit         => MarketerCommissionValue,
        CommissionBasis.PercentOfSale   => SalePrice * MarketerCommissionValue / 100m,
        CommissionBasis.PercentOfProfit => NetProfit * MarketerCommissionValue / 100m,
        _                               => 0m
    };

    private ItineraryProduct() { }

    public static ItineraryProduct Create(int companyId, string name, decimal salePrice, decimal cost,
        int capacity, int? supplierPartyId = null,
        CommissionBasis commissionBasis = CommissionBasis.PercentOfProfit, decimal commissionValue = 0)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("نامِ محصول الزامی است.");
        if (salePrice < 0) throw new ArgumentException("قیمتِ فروش نمی‌تواند منفی باشد.");
        if (cost < 0) throw new ArgumentException("هزینه نمی‌تواند منفی باشد.");
        if (capacity < 0) throw new ArgumentException("ظرفیت نمی‌تواند منفی باشد.");
        if (commissionValue < 0) throw new ArgumentException("پورسانت نمی‌تواند منفی باشد.");
        return new ItineraryProduct
        {
            CompanyId = companyId, Name = name.Trim(), SalePrice = salePrice, Cost = cost,
            Capacity = capacity, SupplierPartyId = supplierPartyId,
            MarketerCommissionBasis = commissionBasis, MarketerCommissionValue = commissionValue
        };
    }

    public void Update(string name, decimal salePrice, decimal cost, int capacity,
        int? supplierPartyId, bool active,
        CommissionBasis commissionBasis, decimal commissionValue, int? userId = null)
    {
        if (!string.IsNullOrWhiteSpace(name)) Name = name.Trim();
        SalePrice = salePrice < 0 ? 0 : salePrice;
        Cost = cost < 0 ? 0 : cost;
        Capacity = capacity < 0 ? 0 : capacity;
        SupplierPartyId = supplierPartyId;
        MarketerCommissionBasis = commissionBasis;
        MarketerCommissionValue = commissionValue < 0 ? 0 : commissionValue;
        Active = active;
        SetAudit(userId);
    }
}
