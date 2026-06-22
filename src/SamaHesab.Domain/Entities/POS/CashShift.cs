using SamaHesab.Domain.Common;

namespace SamaHesab.Domain.Entities.POS;

/// <summary>
/// کار #۳۰ — شیفت/صندوق POS: صندوق‌دار با موجودی اولیه باز می‌کند، فروش‌ها در طول شیفت
/// جمع می‌شوند (نقد/کارت)، و هنگام بستن، مبلغ شمرده‌شده با مبلغِ موردانتظار مقایسه و مغایرت محاسبه می‌شود.
/// </summary>
public class CashShift : AuditableEntity, IBranchScoped   // MB-3: جداسازیِ شعبه
{
    public int BranchId { get; private set; }
    public int UserId { get; private set; }
    public DateTime OpenedAt { get; private set; } = DateTime.Now;
    public DateTime? ClosedAt { get; private set; }
    public int Status { get; private set; }            // 0=باز 1=بسته
    public decimal OpeningFloat { get; private set; }   // موجودی اولیه‌ی صندوق
    public decimal CashSales { get; private set; }
    public decimal CardSales { get; private set; }
    public int SalesCount { get; private set; }
    public decimal CountedCash { get; private set; }    // شمارش نهایی
    public decimal ExpectedCash { get; private set; }   // موردانتظار = موجودی اولیه + فروش نقدی
    public decimal Variance { get; private set; }       // شمارش - موردانتظار (+ اضافه / − کسری)
    public string? Notes { get; private set; }

    public bool IsOpen => Status == 0;

    private CashShift() { }

    public static CashShift Open(int companyId, int branchId, int userId, decimal openingFloat)
    {
        if (openingFloat < 0) throw new ArgumentException("موجودی اولیه نمی‌تواند منفی باشد.");
        return new CashShift
        {
            CompanyId = companyId, BranchId = branchId, UserId = userId,
            OpeningFloat = openingFloat, OpenedAt = DateTime.Now
        };
    }

    /// <summary>ثبت یک فروش در شیفت جاری (Z-report).</summary>
    public void RecordSale(decimal amount, bool isCash)
    {
        if (!IsOpen) throw new InvalidOperationException("شیفت بسته است.");
        if (amount <= 0) return;
        if (isCash) CashSales += amount; else CardSales += amount;
        SalesCount++;
        UpdatedAt = DateTime.Now;
    }

    /// <summary>بستن شیفت با مبلغ شمرده‌شده؛ موردانتظار و مغایرت محاسبه می‌شود.</summary>
    public void Close(decimal countedCash, string? notes = null)
    {
        if (!IsOpen) throw new InvalidOperationException("این شیفت قبلاً بسته شده است.");
        CountedCash = countedCash;
        ExpectedCash = OpeningFloat + CashSales;
        Variance = countedCash - ExpectedCash;
        Notes = notes;
        Status = 1;
        ClosedAt = DateTime.Now;
        UpdatedAt = DateTime.Now;
    }
}
