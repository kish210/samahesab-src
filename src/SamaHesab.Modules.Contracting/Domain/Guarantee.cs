using SamaHesab.Domain.Common;

namespace SamaHesab.Modules.Contracting.Domain;

/// <summary>نوعِ ضمانت‌نامه.</summary>
public enum GuaranteeType { Advance = 0, Performance = 1, Tender = 2 }   // پیش‌پرداخت / حسن‌انجام‌کار / شرکت‌در‌مناقصه

/// <summary>وضعیتِ ضمانت‌نامه.</summary>
public enum GuaranteeStatus { Active = 0, Released = 1, Expired = 2 }

/// <summary>
/// CON-C1-1 — ضمانت‌نامهٔ بانکیِ پیمان (انتظامی). آلارمِ انقضا + آزادسازی.
/// </summary>
public class Guarantee : AuditableEntity
{
    public int ContractProjectId { get; private set; }
    public GuaranteeType Type { get; private set; }
    public string Bank { get; private set; } = default!;
    public decimal Amount { get; private set; }
    public string IssueDate { get; private set; } = default!;   // شمسی
    public string ExpiryDate { get; private set; } = default!;  // شمسی
    public GuaranteeStatus Status { get; private set; } = GuaranteeStatus.Active;
    public string? Note { get; private set; }

    private Guarantee() { }

    public static Guarantee Create(int companyId, int contractProjectId, GuaranteeType type, string bank,
        decimal amount, string issueDate, string expiryDate, string? note = null)
    {
        if (contractProjectId <= 0) throw new ArgumentException("پیمان الزامی است.");
        if (amount <= 0) throw new ArgumentException("مبلغِ ضمانت‌نامه باید بزرگ‌تر از صفر باشد.");
        if (string.IsNullOrWhiteSpace(expiryDate)) throw new ArgumentException("تاریخِ انقضا الزامی است.");
        return new Guarantee
        {
            CompanyId = companyId, ContractProjectId = contractProjectId, Type = type,
            Bank = string.IsNullOrWhiteSpace(bank) ? "—" : bank, Amount = amount,
            IssueDate = issueDate, ExpiryDate = expiryDate, Note = note
        };
    }

    public void Release() { Status = GuaranteeStatus.Released; SetAudit(null); }
    public void MarkExpired() { if (Status == GuaranteeStatus.Active) Status = GuaranteeStatus.Expired; SetAudit(null); }
}
