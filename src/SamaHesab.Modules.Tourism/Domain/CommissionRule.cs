using SamaHesab.Domain.Common;

namespace SamaHesab.Modules.Tourism.Domain;

/// <summary>مبنای محاسبهٔ پورسانت.</summary>
public enum CommissionBasis { PerUnit = 0, PercentOfSale = 1, PercentOfProfit = 2 }

/// <summary>
/// TUR-C1-1 — قاعدهٔ پورسانتِ فروشنده. ترتیبِ تطبیق: (فروشنده+محصول) > (فروشنده+گروه) > (فروشنده+پیش‌فرض).
/// </summary>
public class CommissionRule : AuditableEntity
{
    public int SalespersonPartyId { get; private set; }
    public int? ProductId { get; private set; }
    public int? ProductGroupId { get; private set; }
    public CommissionBasis Basis { get; private set; }
    public decimal Rate { get; private set; }                 // مبلغ (PerUnit) یا درصد (۰..۱ یا ۰..۱۰۰ — در موتور تفسیر می‌شود)
    public bool SaleBaseAfterDiscount { get; private set; }   // مبنای فروش بعد از تخفیف؟
    public string EffectiveFrom { get; private set; } = default!;  // شمسی
    public string? EffectiveTo { get; private set; }              // شمسی (اختیاری)
    public bool Active { get; private set; } = true;

    private CommissionRule() { }

    public static CommissionRule Create(int companyId, int salespersonPartyId, CommissionBasis basis,
        decimal rate, string effectiveFrom, int? productId = null, int? productGroupId = null,
        bool saleBaseAfterDiscount = true, string? effectiveTo = null)
    {
        if (salespersonPartyId <= 0) throw new ArgumentException("فروشنده الزامی است.");
        if (rate < 0) throw new ArgumentException("نرخ نمی‌تواند منفی باشد.");
        if (string.IsNullOrWhiteSpace(effectiveFrom)) throw new ArgumentException("تاریخِ شروعِ اعتبار الزامی است.");
        return new CommissionRule
        {
            CompanyId = companyId, SalespersonPartyId = salespersonPartyId, Basis = basis, Rate = rate,
            ProductId = productId, ProductGroupId = productGroupId,
            SaleBaseAfterDiscount = saleBaseAfterDiscount, EffectiveFrom = effectiveFrom, EffectiveTo = effectiveTo
        };
    }

    public void Update(CommissionBasis basis, decimal rate, int? productId, int? productGroupId,
        bool saleBaseAfterDiscount, string effectiveFrom, string? effectiveTo, bool active)
    {
        Basis = basis; Rate = rate < 0 ? 0 : rate; ProductId = productId; ProductGroupId = productGroupId;
        SaleBaseAfterDiscount = saleBaseAfterDiscount;
        if (!string.IsNullOrWhiteSpace(effectiveFrom)) EffectiveFrom = effectiveFrom;
        EffectiveTo = effectiveTo; Active = active;
        SetAudit(null);
    }
}
