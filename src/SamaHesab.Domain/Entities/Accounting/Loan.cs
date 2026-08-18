using SamaHesab.Domain.Common;
using SamaHesab.Domain.Enums;

namespace SamaHesab.Domain.Entities.Accounting;

/// <summary>
/// تسهیلاتِ مالی/وام (U-LOAN) — ثبتِ اصل، نرخِ بهرهٔ سالانه، مدت و جدولِ اقساط (قسطِ مساوی).
/// هم‌راستا با «تسهیلات مالی»یِ راهکاران: دریافتِ وام سندِ دریافت (بد «نقد/بانک» / بس «وامِ
/// پرداختنی») می‌زند و هر قسط هم سندِ (بد «وامِ پرداختنی» + «هزینهٔ بهره» / بس «نقد/بانک»).
/// </summary>
public class Loan : AuditableEntity
{
    public string Code { get; private set; } = default!;
    /// <summary>نام/طرف‌حسابِ وام (مثلاً «بانک ملت — سرمایه در گردش»).</summary>
    public string Name { get; private set; } = default!;
    /// <summary>تاریخِ دریافتِ وام به‌صورتِ شمسیِ «yyyy/MM/dd».</summary>
    public string StartDate { get; private set; } = default!;
    public decimal Principal { get; private set; }
    /// <summary>نرخِ بهرهٔ سالانه به درصد (مثلاً ۲۳ یعنی ۲۳٪).</summary>
    public decimal AnnualInterestPercent { get; private set; }
    public int TermMonths { get; private set; }
    public LoanStatus Status { get; private set; } = LoanStatus.Active;
    /// <summary>تعدادِ اقساطِ پرداخت‌شده (ایندکسِ آخرین قسطِ ثبت‌شده).</summary>
    public int PaidInstallments { get; private set; }
    public decimal PaidPrincipal { get; private set; }
    public decimal PaidInterest { get; private set; }
    public string? LastPaymentDate { get; private set; }

    private Loan() { }

    public static Loan Create(int companyId, string code, string name, string startDate,
        decimal principal, decimal annualInterestPercent, int termMonths)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("کدِ وام الزامی است.");
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("نامِ وام الزامی است.");
        if (principal <= 0) throw new ArgumentException("اصلِ وام باید بزرگ‌تر از صفر باشد.");
        if (annualInterestPercent < 0) throw new ArgumentException("نرخِ بهره نمی‌تواند منفی باشد.");
        if (termMonths <= 0) throw new ArgumentException("مدتِ وام باید بزرگ‌تر از صفر باشد.");

        return new Loan
        {
            CompanyId = companyId,
            Code = code,
            Name = name,
            StartDate = startDate,
            Principal = principal,
            AnnualInterestPercent = annualInterestPercent,
            TermMonths = termMonths
        };
    }

    /// <summary>ثبتِ یک قسطِ پرداخت‌شده — جمعِ اصل/بهرهٔ پرداختی و شمارندهٔ اقساط را جلو می‌برد.</summary>
    public void RecordPayment(int installmentIndex, decimal principal, decimal interest, string paymentDate)
    {
        if (installmentIndex > PaidInstallments) PaidInstallments = installmentIndex;
        PaidPrincipal += principal;
        PaidInterest += interest;
        LastPaymentDate = paymentDate;
        if (installmentIndex >= TermMonths || RemainingPrincipal <= 0.01m) Status = LoanStatus.Closed;
        UpdatedAt = DateTime.Now;
    }

    public decimal RemainingPrincipal => Math.Max(0, Principal - PaidPrincipal);
}
