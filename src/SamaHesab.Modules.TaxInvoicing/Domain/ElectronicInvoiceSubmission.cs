using SamaHesab.Domain.Common;

namespace SamaHesab.Modules.TaxInvoicing.Domain;

public enum SubmissionStatus { Pending, Sent, Accepted, Rejected, Error }

/// <summary>
/// رکوردِ ارسالِ یک فاکتورِ فروش به «سامانهٔ مودیان و پایانه‌هایِ فروشگاهی» (سازمانِ امورِ مالیاتیِ ایران).
/// الگویِ store-and-forward، عیناً همان الگویِ <c>Support.BugReport</c>: فاکتور محلی صادر می‌شود، این
/// رکورد به‌صورتِ Pending ساخته می‌شود، و بعداً (خودکار یا با تلاشِ مجدد) ارسال می‌شود — قطعیِ اینترنت
/// یا سرورِ سازمان جلویِ صدورِ فاکتورِ داخلی را نمی‌گیرد.
/// رفرنسِ نرم به هستهٔ فروش (فقط <see cref="SalesInvoiceId"/>، بدونِ navigation/JOIN) — هسته هرگز
/// از این ماژول اطلاع ندارد؛ خودِ ماژول با فاکتور از طریقِ Id ارتباط برقرار می‌کند.
/// </summary>
public class ElectronicInvoiceSubmission : AuditableEntity
{
    public int SalesInvoiceId { get; private set; }
    public SubmissionStatus Status { get; private set; } = SubmissionStatus.Pending;

    /// <summary>شناسهٔ یکتایِ مالیاتی (UID، ۲۲ کاراکتری) — فقط پس از پذیرشِ قطعی توسطِ سازمان.</summary>
    public string? UniqueTaxId { get; private set; }
    /// <summary>شمارهٔ مرجعِ پاسخِ سازمان (referenceNumber) — بلافاصله پس از ارسال، پیش از پذیرشِ نهایی.</summary>
    public string? ReferenceNumber { get; private set; }
    public string? ErrorMessage { get; private set; }
    public int RetryCount { get; private set; }
    public DateTime? SentAt { get; private set; }
    public DateTime? LastAttemptAt { get; private set; }

    private ElectronicInvoiceSubmission() { }

    public static ElectronicInvoiceSubmission Create(int companyId, int salesInvoiceId)
    {
        if (salesInvoiceId <= 0) throw new ArgumentException("فاکتورِ فروش الزامی است.");
        return new ElectronicInvoiceSubmission { CompanyId = companyId, SalesInvoiceId = salesInvoiceId };
    }

    /// <summary>بلافاصله پس از ارسالِ موفقِ بستهٔ رمزنگاری‌شده (پیش از پذیرشِ نهایی).</summary>
    public void MarkSent(string referenceNumber)
    {
        Status = SubmissionStatus.Sent;
        ReferenceNumber = referenceNumber;
        SentAt = DateTime.Now;
        LastAttemptAt = DateTime.Now;
        ErrorMessage = null;
        SetAudit(null);
    }

    public void MarkAccepted(string uniqueTaxId)
    {
        Status = SubmissionStatus.Accepted;
        UniqueTaxId = uniqueTaxId;
        LastAttemptAt = DateTime.Now;
        ErrorMessage = null;
        SetAudit(null);
    }

    public void MarkRejected(string reason)
    {
        Status = SubmissionStatus.Rejected;
        ErrorMessage = reason;
        LastAttemptAt = DateTime.Now;
        SetAudit(null);
    }

    /// <summary>خطایِ فنی/شبکه (نه ردِ رسمیِ سازمان) — قابلِ‌تلاشِ‌مجدد، RetryCount شمارش می‌شود.</summary>
    public void MarkError(string reason)
    {
        Status = SubmissionStatus.Error;
        ErrorMessage = reason;
        RetryCount++;
        LastAttemptAt = DateTime.Now;
        SetAudit(null);
    }

    /// <summary>بازگرداندنِ رکوردِ خطاخورده به صفِ ارسال برایِ تلاشِ دستیِ مجدد.</summary>
    public void ResetToPending()
    {
        Status = SubmissionStatus.Pending;
        SetAudit(null);
    }
}
