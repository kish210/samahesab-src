using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.HRM;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Modules.HR.Application;

/// <summary>
/// پیاده‌سازیِ HR از قراردادِ بررسیِ وابستگیِ کارمند: اگر کارمند فیشِ حقوق یا رکوردِ تردد دارد،
/// حذفِ سختش ممنوع است (هسته به‌جای حذف، غیرفعالش می‌کند). با حذفِ ماژولِ HR این چک خودبه‌خود
/// برداشته می‌شود و هسته سالم می‌ماند.
/// </summary>
public sealed class EmployeeDependencyChecker : IEmployeeDependencyChecker
{
    private readonly IRepository<SalarySlip> _slips;
    private readonly IRepository<AttendanceRecord> _attendance;

    public EmployeeDependencyChecker(IRepository<SalarySlip> slips, IRepository<AttendanceRecord> attendance)
    { _slips = slips; _attendance = attendance; }

    public async Task<bool> HasHistoryAsync(int employeeId, CancellationToken ct = default)
        => await _slips.AnyAsync(s => s.EmployeeId == employeeId, ct)
        || await _attendance.AnyAsync(a => a.EmployeeId == employeeId, ct);
}
