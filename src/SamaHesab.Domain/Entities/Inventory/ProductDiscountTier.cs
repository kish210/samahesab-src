using SamaHesab.Domain.Common;

namespace SamaHesab.Domain.Entities.Inventory;

/// <summary>
/// کارِ ۷ (U6) — پلهٔ تخفیفِ مقداریِ یک کالا: «اگر مقدار ≥ MinQty باشد، DiscountPercent درصد تخفیف».
/// بهترین پله (بزرگ‌ترین MinQtyِ کوچک‌تر-مساویِ مقدار) اعمال می‌شود.
/// </summary>
public class ProductDiscountTier : BaseEntity
{
    public int CompanyId { get; private set; }
    public int ProductId { get; private set; }
    public decimal MinQty { get; private set; }
    public decimal DiscountPercent { get; private set; }

    private ProductDiscountTier() { }

    public static ProductDiscountTier Create(int companyId, int productId, decimal minQty, decimal discountPercent)
    {
        if (minQty <= 0) throw new ArgumentException("حداقل مقدار باید بزرگ‌تر از صفر باشد.");
        if (discountPercent < 0 || discountPercent > 100) throw new ArgumentException("درصد تخفیف باید بین ۰ تا ۱۰۰ باشد.");
        return new ProductDiscountTier
        {
            CompanyId = companyId,
            ProductId = productId,
            MinQty = minQty,
            DiscountPercent = discountPercent
        };
    }
}
