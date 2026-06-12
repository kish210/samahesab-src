using SamaHesab.Domain.Common;
namespace SamaHesab.Domain.Entities.Inventory;
public class Batch : BaseEntity
{
    public int ProductId { get; private set; }
    public string BatchNumber { get; private set; } = default!;
    public string? ProductionDate { get; private set; }
    public string? ExpiryDate { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal? PurchasePrice { get; private set; }
    public string? Notes { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.Now;

    private Batch() { }

    public static Batch Create(int productId, string batchNumber, string? productionDate = null,
        string? expiryDate = null, decimal quantity = 0, decimal? purchasePrice = null)
    {
        return new Batch
        {
            ProductId = productId, BatchNumber = batchNumber,
            ProductionDate = productionDate, ExpiryDate = expiryDate,
            Quantity = quantity, PurchasePrice = purchasePrice
        };
    }

    public bool IsExpired(string todayPersianDate) =>
        !string.IsNullOrEmpty(ExpiryDate) && string.Compare(ExpiryDate, todayPersianDate) < 0;

    /// <summary>افزایش موجودی بچ (هنگام رسید/خرید).</summary>
    public void Increase(decimal qty)
    {
        if (qty < 0) throw new ArgumentException("مقدار افزایش نمی‌تواند منفی باشد.");
        Quantity += qty;
    }

    /// <summary>کاهش موجودی بچ (هنگام حواله/فروش). از موجودی بیشتر مجاز نیست.</summary>
    public void Decrease(decimal qty)
    {
        if (qty < 0) throw new ArgumentException("مقدار کاهش نمی‌تواند منفی باشد.");
        if (qty > Quantity) throw new InvalidOperationException("موجودی بچ کافی نیست.");
        Quantity -= qty;
    }
}
