using SamaHesab.Domain.Common;

namespace SamaHesab.Domain.Entities.Contracting;

/// <summary>
/// CON-C1-2 — تنظیماتِ پیمانکاری: نگاشتِ حساب‌های کنترلی به‌ازای نوعِ کسر + درصدهای پیش‌فرضِ سراسری.
/// یک ردیف برای هر شرکت. هیچ AccountId/نرخ هاردکد نمی‌شود (پیش‌فرضِ پروژه بر این override می‌شود).
/// </summary>
public class ContractingSetting : AuditableEntity
{
    // نگاشتِ حساب‌ها
    public int? ReceivableAccountId { get; private set; }         // دریافتنیِ کارفرما
    public int? RetentionDepositAccountId { get; private set; }   // سپردهٔ حسن‌انجام‌کار (دارایی)
    public int? InsuranceDepositAccountId { get; private set; }   // سپردهٔ بیمه (دارایی)
    public int? PrepaidTaxAccountId { get; private set; }         // پیش‌پرداختِ مالیات (دارایی)
    public int? AdvanceLiabilityAccountId { get; private set; }   // بدهیِ پیش‌پرداختِ کارفرما
    public int? PenaltyExpenseAccountId { get; private set; }     // هزینهٔ جریمه
    public int? RevenueAccountId { get; private set; }            // درآمدِ پیمان
    public int? BankAccountId { get; private set; }               // بانک (پیش‌پرداخت/آزادسازیِ سپرده)

    // درصدهای پیش‌فرضِ سراسری (اگر پروژه صفر گذاشت)
    public decimal DefaultAdvancePercent { get; private set; }
    public decimal DefaultRetentionPercent { get; private set; }
    public decimal DefaultInsuranceWithholdPercent { get; private set; }
    public decimal DefaultTaxWithholdPercent { get; private set; }

    public bool UseCostCenterAsDimension { get; private set; }    // بُعدِ پروژه = CostCenter (پیش‌فرض: Project)

    private ContractingSetting() { }

    public static ContractingSetting Create(int companyId) => new() { CompanyId = companyId };

    public void Update(int? receivable, int? retentionDeposit, int? insuranceDeposit, int? prepaidTax,
        int? advanceLiability, int? penaltyExpense, int? revenue, int? bank,
        decimal defaultAdvancePercent, decimal defaultRetentionPercent,
        decimal defaultInsuranceWithholdPercent, decimal defaultTaxWithholdPercent, bool useCostCenterAsDimension)
    {
        ReceivableAccountId = receivable; RetentionDepositAccountId = retentionDeposit;
        InsuranceDepositAccountId = insuranceDeposit; PrepaidTaxAccountId = prepaidTax;
        AdvanceLiabilityAccountId = advanceLiability; PenaltyExpenseAccountId = penaltyExpense;
        RevenueAccountId = revenue; BankAccountId = bank;
        DefaultAdvancePercent = Nn(defaultAdvancePercent); DefaultRetentionPercent = Nn(defaultRetentionPercent);
        DefaultInsuranceWithholdPercent = Nn(defaultInsuranceWithholdPercent);
        DefaultTaxWithholdPercent = Nn(defaultTaxWithholdPercent);
        UseCostCenterAsDimension = useCostCenterAsDimension;
        SetAudit(null);
    }

    private static decimal Nn(decimal v) => v < 0 ? 0 : v;
}
