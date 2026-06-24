using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.HRM;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Modules.Attendance.Application;

/// <summary>
/// چکِ وابستگیِ کارمند از منظرِ حضور: اگر رکوردِ تردد دارد، حذفِ سختش ممنوع است (هسته غیرفعالش می‌کند).
/// همراهِ چکِ حقوقِ ماژولِ HR، هسته همهٔ چک‌کننده‌ها را با‌هم می‌بیند (IEnumerable).
/// </summary>
public sealed class AttendanceEmployeeDependencyChecker : IEmployeeDependencyChecker
{
    private readonly IRepository<AttendanceRecord> _attendance;
    public AttendanceEmployeeDependencyChecker(IRepository<AttendanceRecord> attendance) => _attendance = attendance;

    public Task<bool> HasHistoryAsync(int employeeId, CancellationToken ct = default)
        => _attendance.AnyAsync(a => a.EmployeeId == employeeId, ct);
}
