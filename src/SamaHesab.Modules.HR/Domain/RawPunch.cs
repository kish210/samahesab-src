using SamaHesab.Domain.Common;

namespace SamaHesab.Domain.Entities.HRM;

/// <summary>
/// ATTP-C1-3 — یک ضربهٔ خامِ تردد (از دستگاه/ایمپورت). با پردازش (جفت‌سازیِ اولین=ورود/آخرین=خروج) به
/// AttendanceRecordِ روزانه تبدیل و Processed علامت می‌خورد.
/// </summary>
public class RawPunch : AuditableEntity
{
    public int EmployeeId { get; private set; }
    public int? DeviceId { get; private set; }
    public string WorkDate { get; private set; } = default!;   // شمسی «YYYY/MM/DD»
    public TimeOnly PunchTime { get; private set; }
    public bool Processed { get; private set; }

    private RawPunch() { }

    public static RawPunch Create(int companyId, int employeeId, string workDate, TimeOnly punchTime, int? deviceId = null)
    {
        if (employeeId <= 0) throw new ArgumentException("کارمند الزامی است.");
        if (string.IsNullOrWhiteSpace(workDate)) throw new ArgumentException("تاریخ الزامی است.");
        return new RawPunch
        {
            CompanyId = companyId, EmployeeId = employeeId, WorkDate = workDate,
            PunchTime = punchTime, DeviceId = deviceId
        };
    }

    public void MarkProcessed() { Processed = true; SetAudit(null); }
}
