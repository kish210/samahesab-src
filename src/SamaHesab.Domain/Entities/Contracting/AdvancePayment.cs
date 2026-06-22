using SamaHesab.Domain.Common;

namespace SamaHesab.Domain.Entities.Contracting;

/// <summary>
/// CON-C1-1 — پیش‌پرداختِ دریافتی از کارفرما (بدهیِ پیمانکار). در صورت‌وضعیت‌ها به‌مرور بازیافت می‌شود
/// (سقف: کلِ بازیافت ≤ مبلغِ دریافتی). RecoveredToDate جمعِ بازیافتِ تاکنون است.
/// </summary>
public class AdvancePayment : AuditableEntity
{
    public int ContractProjectId { get; private set; }
    public decimal Amount { get; private set; }
    public string Date { get; private set; } = default!;   // شمسی
    public decimal RecoveredToDate { get; private set; }
    public string PaymentMethod { get; private set; } = "بانک";
    public int? VoucherId { get; private set; }
    public string? Note { get; private set; }

    /// <summary>ماندهٔ بازیافت‌نشدهٔ پیش‌پرداخت.</summary>
    public decimal Outstanding => Amount - RecoveredToDate;

    private AdvancePayment() { }

    public static AdvancePayment Create(int companyId, int contractProjectId, decimal amount, string date,
        string paymentMethod = "بانک", string? note = null)
    {
        if (contractProjectId <= 0) throw new ArgumentException("پیمان الزامی است.");
        if (amount <= 0) throw new ArgumentException("مبلغِ پیش‌پرداخت باید بزرگ‌تر از صفر باشد.");
        if (string.IsNullOrWhiteSpace(date)) throw new ArgumentException("تاریخ الزامی است.");
        return new AdvancePayment
        {
            CompanyId = companyId, ContractProjectId = contractProjectId, Amount = amount,
            Date = date, PaymentMethod = paymentMethod, Note = note
        };
    }

    public void SetVoucher(int voucherId) { VoucherId = voucherId; SetAudit(null); }

    /// <summary>افزایشِ بازیافتِ تجمعی (با سقفِ مبلغِ دریافتی). مقدارِ واقعیِ اعمال‌شده را برمی‌گرداند.</summary>
    public decimal Recover(decimal amount)
    {
        if (amount <= 0) return 0;
        var applied = Math.Min(amount, Outstanding);
        RecoveredToDate += applied;
        SetAudit(null);
        return applied;
    }
}
