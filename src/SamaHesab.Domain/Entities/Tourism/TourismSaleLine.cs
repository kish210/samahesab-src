using SamaHesab.Domain.Common;

namespace SamaHesab.Domain.Entities.Tourism;

/// <summary>
/// TUR-C1-1 — خطِ فروش. SupplierPartyId و UnitCost به‌صورتِ snapshot ذخیره می‌شوند (مستقل از تغییرِ بعدیِ محصول).
/// سود = تعداد×قیمتِ‌فروش − تخفیف − تعداد×بهای‌تمام‌شده.
/// </summary>
public class TourismSaleLine : BaseEntity
{
    public int SaleId { get; private set; }
    public int ProductId { get; private set; }
    public int SupplierPartyId { get; private set; }      // snapshot
    public decimal Quantity { get; private set; }
    public decimal UnitSalePrice { get; private set; }
    public decimal DiscountAmount { get; private set; }
    public decimal UnitCost { get; private set; }         // snapshot
    public decimal LineProfit { get; private set; }
    /// <summary>تاریخِ سفر/استفاده (شمسی، اختیاری) — برای نمایش در ردیف و چاپِ واچر.</summary>
    public string? TravelDate { get; private set; }

    private readonly List<SalePassenger> _passengers = new();
    public IReadOnlyCollection<SalePassenger> Passengers => _passengers.AsReadOnly();

    private TourismSaleLine() { }

    public static TourismSaleLine Create(int productId, int supplierPartyId, decimal quantity,
        decimal unitSalePrice, decimal discountAmount, decimal unitCost, string? travelDate = null)
    {
        if (productId <= 0) throw new ArgumentException("محصول الزامی است.");
        if (quantity <= 0) throw new ArgumentException("تعداد باید بزرگ‌تر از صفر باشد.");
        if (unitSalePrice < 0 || unitCost < 0 || discountAmount < 0)
            throw new ArgumentException("مبالغ نمی‌توانند منفی باشند.");
        var profit = quantity * unitSalePrice - discountAmount - quantity * unitCost;
        return new TourismSaleLine
        {
            ProductId = productId, SupplierPartyId = supplierPartyId, Quantity = quantity,
            UnitSalePrice = unitSalePrice, DiscountAmount = discountAmount, UnitCost = unitCost,
            LineProfit = profit, TravelDate = travelDate
        };
    }

    public void AddPassenger(SalePassenger p) => _passengers.Add(p);
}
