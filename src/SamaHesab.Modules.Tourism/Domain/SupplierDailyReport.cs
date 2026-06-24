using SamaHesab.Domain.Common;

namespace SamaHesab.Modules.Tourism.Domain;

/// <summary>وضعیتِ گزارشِ روزانهٔ تأمین‌کننده.</summary>
public enum DailyReportStatus { Draft = 0, Sent = 1, Reconciled = 2 }

/// <summary>
/// TUR-C1-1/5 — گزارشِ روزانهٔ تأمین‌کننده: جمعِ فروش‌های آن روزِ آن تأمین‌کننده (با لیستِ مسافر) که برایش ارسال می‌شود.
/// TotalCost = برداشتِ ودیعهٔ آن روز. Reconcile: مبلغِ کسرِ واقعیِ تأمین‌کننده ثبت و اختلاف به سندِ تعدیل می‌رود.
/// خطوط/مسافران زنده از TourismSaleLine خوانده می‌شوند (snapshotِ آماری اینجا).
/// </summary>
public class SupplierDailyReport : AuditableEntity
{
    public int SupplierPartyId { get; private set; }
    public string Date { get; private set; } = default!;   // شمسی
    public decimal TotalCost { get; private set; }         // برداشتِ ودیعهٔ ثبت‌شدهٔ ما
    public int LineCount { get; private set; }
    public int PassengerCount { get; private set; }
    public DailyReportStatus Status { get; private set; } = DailyReportStatus.Draft;
    public decimal? SupplierDeductedAmount { get; private set; }   // مبلغِ کسرِ واقعیِ تأمین‌کننده (آشتی)
    public int? AdjustmentVoucherId { get; private set; }          // سندِ تعدیلِ اختلاف
    public string? Note { get; private set; }

    private SupplierDailyReport() { }

    public static SupplierDailyReport Create(int companyId, int supplierPartyId, string date,
        decimal totalCost, int lineCount, int passengerCount)
    {
        if (supplierPartyId <= 0) throw new ArgumentException("تأمین‌کننده الزامی است.");
        if (string.IsNullOrWhiteSpace(date)) throw new ArgumentException("تاریخ الزامی است.");
        return new SupplierDailyReport
        {
            CompanyId = companyId, SupplierPartyId = supplierPartyId, Date = date,
            TotalCost = totalCost, LineCount = lineCount, PassengerCount = passengerCount
        };
    }

    public void MarkSent() { if (Status == DailyReportStatus.Draft) Status = DailyReportStatus.Sent; SetAudit(null); }

    /// <summary>ثبتِ کسرِ تأمین‌کننده و علامتِ آشتی (سندِ تعدیل در صورتِ اختلاف توسطِ کامند زده می‌شود).</summary>
    public void Reconcile(decimal supplierDeductedAmount, int? adjustmentVoucherId, string? note = null)
    {
        SupplierDeductedAmount = supplierDeductedAmount;
        AdjustmentVoucherId = adjustmentVoucherId;
        Status = DailyReportStatus.Reconciled;
        if (note is not null) Note = note;
        SetAudit(null);
    }
}
