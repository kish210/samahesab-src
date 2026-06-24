using SamaHesab.Domain.Common;

namespace SamaHesab.Modules.Tourism.Domain;

/// <summary>
/// TUR-C1-1 — رکوردِ پورسانتِ یک خطِ فروش (یکی به‌ازای هر خط). در پایانِ ماه per-فروشنده تجمیع و
/// به‌عنوانِ «پورسانت فروش» به فیشِ حقوق تزریق می‌شود (TUR-C1-6). PersianYearMonth = «YYYYMM»ِ شمسی.
/// </summary>
public class SalesCommissionEntry : AuditableEntity
{
    public int SaleLineId { get; private set; }
    public int SalespersonPartyId { get; private set; }
    public int? RuleId { get; private set; }
    public CommissionBasis Basis { get; private set; }
    public decimal BaseAmount { get; private set; }       // مبنای محاسبه (فروش/سود/تعداد)
    public decimal Rate { get; private set; }
    public decimal CommissionAmount { get; private set; }
    public string PersianYearMonth { get; private set; } = default!;  // «YYYYMM»

    private SalesCommissionEntry() { }

    public static SalesCommissionEntry Create(int companyId, int saleLineId, int salespersonPartyId,
        CommissionBasis basis, decimal baseAmount, decimal rate, decimal commissionAmount,
        string persianYearMonth, int? ruleId = null)
    {
        if (saleLineId <= 0) throw new ArgumentException("خطِ فروش الزامی است.");
        if (salespersonPartyId <= 0) throw new ArgumentException("فروشنده الزامی است.");
        return new SalesCommissionEntry
        {
            CompanyId = companyId, SaleLineId = saleLineId, SalespersonPartyId = salespersonPartyId,
            Basis = basis, BaseAmount = baseAmount, Rate = rate, CommissionAmount = commissionAmount,
            PersianYearMonth = persianYearMonth, RuleId = ruleId
        };
    }
}
