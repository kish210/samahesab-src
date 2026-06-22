using SamaHesab.Domain.Common;

namespace SamaHesab.Domain.Entities.Contracting;

/// <summary>نوعِ صورت‌وضعیت.</summary>
public enum StatementType { Interim = 0, Final = 1 }   // موقت / قطعی

/// <summary>وضعیتِ صورت‌وضعیت.</summary>
public enum StatementStatus { Draft = 0, Approved = 1, Posted = 2 }

/// <summary>
/// CON-C1-1 — صورت‌وضعیت (Progress Statement). آبشارِ محاسبه توسطِ موتورِ CON-C2-1 پر می‌شود؛
/// در Approve→Post یک سندِ متوازن می‌زند. مقادیرِ محاسبه‌شده به‌صورتِ ستون ذخیره می‌شوند (برای گزارش/چاپ).
/// </summary>
public class ProgressStatement : AuditableEntity
{
    public int ContractProjectId { get; private set; }
    public int Number { get; private set; }
    public StatementType Type { get; private set; }
    public string Date { get; private set; } = default!;   // شمسی

    public decimal CumulativeGrossWork { get; private set; }
    public decimal PreviousCumulative { get; private set; }
    public decimal PeriodWork { get; private set; }
    public decimal AdjustmentAmount { get; private set; }       // تعدیل
    public decimal MaterialDiffAmount { get; private set; }     // مابه‌التفاوتِ مصالح
    public decimal GrossThisPeriod { get; private set; }

    // کسورات (محاسبه‌شده)
    public decimal AdvanceRecovery { get; private set; }
    public decimal Retention { get; private set; }
    public decimal Insurance { get; private set; }
    public decimal Tax { get; private set; }
    public decimal Penalty { get; private set; }
    public decimal Other { get; private set; }
    public decimal NetPayable { get; private set; }

    public StatementStatus Status { get; private set; } = StatementStatus.Draft;
    public int? VoucherId { get; private set; }

    private readonly List<StatementDeduction> _deductions = new();
    public IReadOnlyCollection<StatementDeduction> Deductions => _deductions.AsReadOnly();

    private ProgressStatement() { }

    public static ProgressStatement Create(int companyId, int contractProjectId, int number, StatementType type,
        string date, decimal cumulativeGrossWork, decimal previousCumulative,
        decimal adjustmentAmount = 0, decimal materialDiffAmount = 0)
    {
        if (contractProjectId <= 0) throw new ArgumentException("پیمان الزامی است.");
        if (string.IsNullOrWhiteSpace(date)) throw new ArgumentException("تاریخ الزامی است.");
        return new ProgressStatement
        {
            CompanyId = companyId, ContractProjectId = contractProjectId, Number = number, Type = type, Date = date,
            CumulativeGrossWork = cumulativeGrossWork, PreviousCumulative = previousCumulative,
            AdjustmentAmount = adjustmentAmount, MaterialDiffAmount = materialDiffAmount
        };
    }

    /// <summary>ثبتِ نتیجهٔ آبشارِ محاسبه (از موتورِ CON-C2-1).</summary>
    public void SetComputed(decimal periodWork, decimal grossThisPeriod, decimal advanceRecovery, decimal retention,
        decimal insurance, decimal tax, decimal penalty, decimal other, decimal netPayable)
    {
        PeriodWork = periodWork; GrossThisPeriod = grossThisPeriod;
        AdvanceRecovery = advanceRecovery; Retention = retention; Insurance = insurance; Tax = tax;
        Penalty = penalty; Other = other; NetPayable = netPayable;
        SetAudit(null);
    }

    public void AddDeduction(StatementDeduction d) => _deductions.Add(d);
    public void ClearDeductions() => _deductions.Clear();

    public void Approve() { if (Status == StatementStatus.Draft) Status = StatementStatus.Approved; SetAudit(null); }

    public void MarkPosted(int voucherId)
    {
        Status = StatementStatus.Posted; VoucherId = voucherId; SetAudit(null);
    }
}
