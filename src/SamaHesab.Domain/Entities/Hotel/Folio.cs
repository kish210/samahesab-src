using SamaHesab.Domain.Common;

namespace SamaHesab.Domain.Entities.Hotel;

public enum FolioStatus { Open = 0, Closed = 1, Settled = 2 }

/// <summary>نوعِ شارژِ فولیو — درآمدزا (Room/ExtraBed/Restaurant/…)، عوارض، یا تخفیف.</summary>
public enum FolioChargeType { Room = 0, RoomLevy = 1, ExtraBed = 2, Restaurant = 3, Minibar = 4, Laundry = 5, Telephone = 6, Damage = 7, Misc = 8, Discount = 9 }

public enum FolioPaymentMethod { Cash = 0, Card = 1, Transfer = 2, Cheque = 3, AgentBillTo = 4 }

/// <summary>PMS-C1-1 — صورتحسابِ مهمان (فولیو): شارژها + پرداخت‌ها + اعمالِ ودیعه؛ مانده تا تسویه.</summary>
public class Folio : AuditableEntity
{
    public int ReservationId { get; private set; }
    public int? RoomId { get; private set; }
    public string OpenDate { get; private set; } = default!;   // شمسی
    public string? CloseDate { get; private set; }
    public FolioStatus Status { get; private set; } = FolioStatus.Open;
    public decimal TotalCharges { get; private set; }
    public decimal TotalPayments { get; private set; }
    public decimal AppliedDeposit { get; private set; }

    /// <summary>ماندهٔ قابلِ‌پرداختِ مهمان = شارژها − (پرداخت‌ها + ودیعهٔ اعمال‌شده). محاسباتی (EF: Ignore).</summary>
    public decimal Balance => TotalCharges - TotalPayments - AppliedDeposit;
    /// <summary>آیا هنوز قابلِ افزودنِ شارژ/پرداخت است؟ محاسباتی (EF: Ignore).</summary>
    public bool IsChargeable => Status == FolioStatus.Open;

    private readonly List<FolioCharge> _charges = new();
    public IReadOnlyCollection<FolioCharge> Charges => _charges.AsReadOnly();
    private readonly List<FolioPayment> _payments = new();
    public IReadOnlyCollection<FolioPayment> Payments => _payments.AsReadOnly();

    private Folio() { }

    public static Folio Create(int companyId, int reservationId, string openDate, int? roomId = null)
    {
        if (reservationId <= 0) throw new ArgumentException("رزرو الزامی است.");
        if (string.IsNullOrWhiteSpace(openDate)) throw new ArgumentException("تاریخِ بازشدن الزامی است.");
        return new Folio { CompanyId = companyId, ReservationId = reservationId, OpenDate = openDate, RoomId = roomId };
    }

    public FolioCharge AddCharge(FolioChargeType type, decimal amount, string description, string date)
    {
        if (!IsChargeable) throw new InvalidOperationException("فولیوِ بسته/تسویه‌شده را نمی‌توان شارژ کرد.");
        if (amount < 0) throw new ArgumentException("مبلغِ شارژ نمی‌تواند منفی باشد.");
        var c = FolioCharge.Create(type, amount, description, date);
        _charges.Add(c);
        // تخفیف، شارژ را کاهش می‌دهد.
        TotalCharges += (type == FolioChargeType.Discount ? -amount : amount);
        SetAudit(null);
        return c;
    }

    public FolioPayment AddPayment(FolioPaymentMethod method, decimal amount, string description, string date)
    {
        if (!IsChargeable) throw new InvalidOperationException("فولیوِ بسته/تسویه‌شده را نمی‌توان پرداخت کرد.");
        if (amount <= 0) throw new ArgumentException("مبلغِ پرداخت باید مثبت باشد.");
        var p = FolioPayment.Create(method, amount, description, date);
        _payments.Add(p);
        TotalPayments += amount;
        SetAudit(null);
        return p;
    }

    /// <summary>اعمالِ ودیعهٔ از-پیش-دریافت‌شده روی فولیو (کاهشِ مانده؛ خودِ سندِ ودیعه جای دیگری زده می‌شود).</summary>
    public void ApplyDeposit(decimal amount)
    {
        if (!IsChargeable) throw new InvalidOperationException("فولیوِ بسته/تسویه‌شده ودیعه نمی‌پذیرد.");
        if (amount <= 0) throw new ArgumentException("مبلغِ ودیعه باید مثبت باشد.");
        AppliedDeposit += amount;
        SetAudit(null);
    }

    public void Close(string date)
    {
        if (Status != FolioStatus.Open) return;
        Status = FolioStatus.Closed; CloseDate = date; SetAudit(null);
    }

    public void Settle()
    {
        if (Status == FolioStatus.Settled) return;
        Status = FolioStatus.Settled; SetAudit(null);
    }
}

/// <summary>ردیفِ شارژِ فولیو.</summary>
public class FolioCharge : BaseEntity
{
    public int FolioId { get; private set; }
    public FolioChargeType Type { get; private set; }
    public decimal Amount { get; private set; }
    public string Description { get; private set; } = default!;
    public string Date { get; private set; } = default!;   // شمسی

    private FolioCharge() { }

    public static FolioCharge Create(FolioChargeType type, decimal amount, string description, string date)
        => new FolioCharge { Type = type, Amount = amount, Description = description ?? string.Empty, Date = date };
}

/// <summary>ردیفِ پرداختِ فولیو.</summary>
public class FolioPayment : BaseEntity
{
    public int FolioId { get; private set; }
    public FolioPaymentMethod Method { get; private set; }
    public decimal Amount { get; private set; }
    public string Description { get; private set; } = default!;
    public string Date { get; private set; } = default!;   // شمسی

    private FolioPayment() { }

    public static FolioPayment Create(FolioPaymentMethod method, decimal amount, string description, string date)
        => new FolioPayment { Method = method, Amount = amount, Description = description ?? string.Empty, Date = date };
}
