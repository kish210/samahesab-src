using SamaHesab.Domain.Common;

namespace SamaHesab.Domain.Entities.Contracting;

/// <summary>نوعِ کسرِ صورت‌وضعیت.</summary>
public enum DeductionType { AdvanceRecovery = 0, Retention = 1, Insurance = 2, Tax = 3, Penalty = 4, Other = 5 }

/// <summary>
/// CON-C1-1 — یک ردیفِ کسرِ صورت‌وضعیت (پایه × نرخ = مبلغ) با حسابِ مقصد (از تنظیمات).
/// هنگامِ Post برای ساختِ خطوطِ سند و ممیزی تولید می‌شود.
/// </summary>
public class StatementDeduction : BaseEntity
{
    public int StatementId { get; private set; }
    public DeductionType Type { get; private set; }
    public decimal Base { get; private set; }
    public decimal Rate { get; private set; }
    public decimal Amount { get; private set; }
    public int AccountId { get; private set; }

    private StatementDeduction() { }

    public static StatementDeduction Create(DeductionType type, decimal @base, decimal rate, decimal amount, int accountId)
        => new() { Type = type, Base = @base, Rate = rate, Amount = amount, AccountId = accountId };
}
