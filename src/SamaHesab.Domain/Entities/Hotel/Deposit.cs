using SamaHesab.Domain.Common;

namespace SamaHesab.Domain.Entities.Hotel;

public enum DepositStatus { Held = 0, Applied = 1, Refunded = 2 }

/// <summary>PMS-C1-1 — ودیعه/پیش‌پرداختِ رزرو. هنگام دریافت بدهیِ ودیعه می‌خورد؛ هنگام check-in روی فولیو اعمال یا بازپرداخت می‌شود.</summary>
public class Deposit : AuditableEntity
{
    public int ReservationId { get; private set; }
    public decimal Amount { get; private set; }
    public decimal AppliedAmount { get; private set; }
    public string Date { get; private set; } = default!;   // شمسی
    public DepositStatus Status { get; private set; } = DepositStatus.Held;
    public int? VoucherId { get; private set; }

    /// <summary>ماندهٔ بلااستفادهٔ ودیعه. محاسباتی (EF: Ignore).</summary>
    public decimal Remaining => Amount - AppliedAmount;

    private Deposit() { }

    public static Deposit Create(int companyId, int reservationId, decimal amount, string date)
    {
        if (reservationId <= 0) throw new ArgumentException("رزرو الزامی است.");
        if (amount <= 0) throw new ArgumentException("مبلغِ ودیعه باید مثبت باشد.");
        if (string.IsNullOrWhiteSpace(date)) throw new ArgumentException("تاریخ الزامی است.");
        return new Deposit { CompanyId = companyId, ReservationId = reservationId, Amount = amount, Date = date };
    }

    public void Apply(decimal amount)
    {
        if (Status == DepositStatus.Refunded) throw new InvalidOperationException("ودیعهٔ بازپرداخت‌شده قابلِ اعمال نیست.");
        if (amount <= 0) throw new ArgumentException("مبلغِ اعمال باید مثبت باشد.");
        if (amount > Remaining) throw new InvalidOperationException("مبلغِ اعمال از ماندهٔ ودیعه بیشتر است.");
        AppliedAmount += amount;
        if (Remaining == 0) Status = DepositStatus.Applied;
        SetAudit(null);
    }

    public void Refund()
    {
        if (Status == DepositStatus.Refunded) return;
        Status = DepositStatus.Refunded; SetAudit(null);
    }

    public void SetVoucher(int voucherId) { VoucherId = voucherId; SetAudit(null); }
}
