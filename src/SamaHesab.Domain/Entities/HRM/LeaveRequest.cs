using SamaHesab.Domain.Common;

namespace SamaHesab.Domain.Entities.HRM;

/// <summary>
/// ATT-C1-1 — درخواستِ مرخصی (استحقاقی/استعلاجی/بدونِ‌حقوق/ساعتی) با چرخهٔ تأیید.
/// مصرفِ تأییدشده به موتورِ ماندهٔ مرخصی (ATT-C2-2) و تجمیعِ ماهانه (ATT-C1-3) خوراک می‌دهد.
/// </summary>
public class LeaveRequest : BaseEntity
{
    // نوعِ مرخصی
    public const string TypeAnnual = "استحقاقی";
    public const string TypeSick = "استعلاجی";
    public const string TypeUnpaid = "بدونِ‌حقوق";
    public const string TypeHourly = "ساعتی";
    // وضعیت
    public const string StatusPending = "درخواست";
    public const string StatusApproved = "تأییدشده";
    public const string StatusRejected = "ردشده";

    public int CompanyId { get; private set; }
    public int EmployeeId { get; private set; }
    public string LeaveType { get; private set; } = TypeAnnual;
    public string StartDate { get; private set; } = default!;   // شمسی
    public string EndDate { get; private set; } = default!;     // شمسی (برای ساعتی = همان روز)
    public decimal Days { get; private set; }                   // تعدادِ روز (۰ برای مرخصیِ ساعتی)
    public decimal Hours { get; private set; }                  // ساعت (برای مرخصیِ ساعتی)
    public string Status { get; private set; } = StatusPending;
    public string? Reason { get; private set; }
    public int? DecidedBy { get; private set; }
    public string? DecisionDate { get; private set; }
    public string? DecisionNote { get; private set; }

    private LeaveRequest() { }

    public static LeaveRequest Create(int companyId, int employeeId, string leaveType,
        string startDate, string endDate, decimal days, decimal hours = 0, string? reason = null)
    {
        if (employeeId <= 0) throw new ArgumentException("کارمند الزامی است.");
        if (string.IsNullOrWhiteSpace(startDate)) throw new ArgumentException("تاریخِ شروع الزامی است.");
        var type = string.IsNullOrWhiteSpace(leaveType) ? TypeAnnual : leaveType;
        var isHourly = type == TypeHourly;
        if (isHourly && hours <= 0) throw new ArgumentException("ساعتِ مرخصیِ ساعتی الزامی است.");
        if (!isHourly && days <= 0) throw new ArgumentException("تعدادِ روزِ مرخصی الزامی است.");

        return new LeaveRequest
        {
            CompanyId = companyId, EmployeeId = employeeId, LeaveType = type,
            StartDate = startDate, EndDate = string.IsNullOrWhiteSpace(endDate) ? startDate : endDate,
            Days = isHourly ? 0 : days, Hours = isHourly ? hours : 0, Reason = reason
        };
    }

    public void Approve(int decidedBy, string decisionDate, string? note = null)
    {
        if (Status != StatusPending) throw new InvalidOperationException("فقط درخواستِ در انتظار قابلِ تأیید است.");
        Status = StatusApproved; DecidedBy = decidedBy; DecisionDate = decisionDate; DecisionNote = note;
    }

    public void Reject(int decidedBy, string decisionDate, string? note = null)
    {
        if (Status != StatusPending) throw new InvalidOperationException("فقط درخواستِ در انتظار قابلِ رد است.");
        Status = StatusRejected; DecidedBy = decidedBy; DecisionDate = decisionDate; DecisionNote = note;
    }
}
