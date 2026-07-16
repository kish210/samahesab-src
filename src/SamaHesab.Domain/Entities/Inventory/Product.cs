using SamaHesab.Domain.Common;
using SamaHesab.Domain.Enums;

namespace SamaHesab.Domain.Entities.Inventory;

public class Product : AuditableEntity
{
    public string Code { get; private set; } = default!;
    public string? Barcode { get; private set; }
    public string? Barcode2 { get; private set; }
    public string Name { get; private set; } = default!;
    public string? NameEn { get; private set; }
    public int? GroupId { get; private set; }
    public int? BrandId { get; private set; }
    public int UnitId { get; private set; }
    public int? Unit2Id { get; private set; }
    public decimal? Unit2Factor { get; private set; }
    public ProductType ProductType { get; private set; } = ProductType.Product;
    public decimal PurchasePrice { get; private set; }
    public decimal SalePrice { get; private set; }
    public decimal WholesalePrice { get; private set; }
    public decimal ConsumerPrice { get; private set; }
    public decimal MinStock { get; private set; }
    public decimal? MaxStock { get; private set; }
    public decimal? ReorderPoint { get; private set; }
    public bool HasSerial { get; private set; }
    public bool HasBatch { get; private set; }
    public bool HasExpiry { get; private set; }
    public ValuationMethod ValuationMethod { get; private set; } = ValuationMethod.WeightedAverage;
    public decimal TaxRate { get; private set; }
    public byte[]? Image { get; private set; }
    public string? Description { get; private set; }
    public bool IsActive { get; private set; } = true;
    /// <summary>U-BRANCH-BASEDATA — شعبهٔ اختصاصیِ کالا (null = مشترکِ همهٔ شعب).</summary>
    public int? BranchId { get; private set; }

    public ICollection<StockItem> StockItems { get; private set; } = new List<StockItem>();
    public ICollection<ProductPriceLevel> PriceLevels { get; private set; } = new List<ProductPriceLevel>();

    private Product() { }

    public static Product Create(int companyId, string code, string name, int unitId,
        decimal salePrice, decimal purchasePrice, ProductType productType = ProductType.Product)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("کد کالا الزامی است.");
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("نام کالا الزامی است.");
        if (salePrice < 0) throw new ArgumentException("قیمت فروش نمی‌تواند منفی باشد.");

        return new Product
        {
            CompanyId = companyId,
            Code = code,
            Name = name,
            UnitId = unitId,
            SalePrice = salePrice,
            PurchasePrice = purchasePrice,
            ProductType = productType,
            WholesalePrice = purchasePrice,
            ConsumerPrice = salePrice
        };
    }

    public void UpdatePrices(decimal purchasePrice, decimal salePrice,
        decimal wholesalePrice, decimal consumerPrice, decimal taxRate)
    {
        PurchasePrice = purchasePrice;
        SalePrice = salePrice;
        WholesalePrice = wholesalePrice;
        ConsumerPrice = consumerPrice;
        TaxRate = taxRate;
        UpdatedAt = DateTime.Now;
    }

    public void UpdateDetails(string name, string? nameEn, int? groupId, int? brandId,
        string? barcode, string? barcode2, string? description)
    {
        Name = name;
        NameEn = nameEn;
        GroupId = groupId;
        BrandId = brandId;
        Barcode = barcode;
        Barcode2 = barcode2;
        Description = description;
        UpdatedAt = DateTime.Now;
    }

    public void SetStockLimits(decimal minStock, decimal? maxStock, decimal? reorderPoint)
    {
        MinStock = minStock;
        MaxStock = maxStock;
        ReorderPoint = reorderPoint;
        UpdatedAt = DateTime.Now;
    }

    public void SetTrackingOptions(bool hasSerial, bool hasBatch, bool hasExpiry,
        ValuationMethod valuationMethod)
    {
        HasSerial = hasSerial;
        HasBatch = hasBatch;
        HasExpiry = hasExpiry;
        ValuationMethod = valuationMethod;
        UpdatedAt = DateTime.Now;
    }

    public void SetImage(byte[] image) { Image = image; UpdatedAt = DateTime.Now; }

    public decimal GetPriceForLevel(string priceLevel) => priceLevel switch
    {
        "خرده" => SalePrice,
        "عمده" => WholesalePrice,
        "مصرف‌کننده" => ConsumerPrice,
        _ => SalePrice
    };

    public void Deactivate() { IsActive = false; UpdatedAt = DateTime.Now; }
    public void Activate() { IsActive = true; UpdatedAt = DateTime.Now; }
    public void SetBranch(int? branchId) { BranchId = branchId; UpdatedAt = DateTime.Now; }
}
