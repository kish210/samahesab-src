using SamaHesab.Domain.Common;

namespace SamaHesab.Modules.Tourism.Domain;

/// <summary>
/// TUR-C1-1/2 — تنظیماتِ گردشگری: نگاشتِ حساب‌های کنترلی (هیچ AccountId هاردکد نشود)
/// + پرچم‌های رفتاری. یک ردیف به‌ازای هر شرکت. هم‌الگوی PayrollSetting.
/// </summary>
public class TourismSetting : AuditableEntity
{
    // نگاشتِ حساب‌ها (AccountId از صفحهٔ تنظیمات)
    public int? CashAccountId { get; private set; }              // صندوق/بانک (دریافتِ نقدی)
    public int? ReceivableAccountId { get; private set; }        // دریافتنی مشتری
    public int? RevenueAccountId { get; private set; }           // درآمدِ فروشِ خدماتِ گردشگری
    public int? CogsAccountId { get; private set; }              // بهای تمام‌شدهٔ خدمات
    public int? SupplierDepositAccountId { get; private set; }   // ودیعه نزد تأمین‌کننده (دارایی، کنترلی)
    public int? SalesDiscountAccountId { get; private set; }     // تخفیفِ فروش (کاهندهٔ درآمد)
    public int? DepositDifferenceAccountId { get; private set; } // اختلافِ ودیعه (آشتی)
    public int? CommissionExpenseAccountId { get; private set; } // هزینهٔ پورسانت (مدلِ مستقل)
    public int? SalespersonPayableAccountId { get; private set; }// پرداختنی به فروشنده (مدلِ مستقل)
    public int? BankAccountId { get; private set; }              // بانک برای شارژِ ودیعه

    // پرچم‌ها
    public bool SaleBaseAfterDiscountDefault { get; private set; } = true;
    public decimal LowDepositThreshold { get; private set; }     // آستانهٔ آلارمِ ودیعهٔ کم
    public bool PostPerSale { get; private set; } = true;        // ثبتِ COGS/برداشت per-sale (پیش‌فرض) یا هنگامِ گزارشِ روزانه
    public bool CommissionThroughPayroll { get; private set; } = true; // پورسانت از حقوق یا مستقل

    private TourismSetting() { }

    public static TourismSetting Create(int companyId) => new() { CompanyId = companyId };

    public void Update(int? cash, int? receivable, int? revenue, int? cogs, int? supplierDeposit,
        int? salesDiscount, int? depositDifference, int? commissionExpense, int? salespersonPayable, int? bank,
        bool saleBaseAfterDiscountDefault, decimal lowDepositThreshold, bool postPerSale, bool commissionThroughPayroll)
    {
        CashAccountId = cash; ReceivableAccountId = receivable; RevenueAccountId = revenue; CogsAccountId = cogs;
        SupplierDepositAccountId = supplierDeposit; SalesDiscountAccountId = salesDiscount;
        DepositDifferenceAccountId = depositDifference; CommissionExpenseAccountId = commissionExpense;
        SalespersonPayableAccountId = salespersonPayable; BankAccountId = bank;
        SaleBaseAfterDiscountDefault = saleBaseAfterDiscountDefault;
        LowDepositThreshold = lowDepositThreshold < 0 ? 0 : lowDepositThreshold;
        PostPerSale = postPerSale; CommissionThroughPayroll = commissionThroughPayroll;
        SetAudit(null);
    }
}
