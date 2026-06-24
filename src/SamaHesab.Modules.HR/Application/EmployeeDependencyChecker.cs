using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.HRM;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Modules.HR.Application;

/// <summary>
/// چکِ وابستگیِ کارمند از منظرِ حقوق: اگر فیشِ حقوق دارد، حذفِ سختش ممنوع است (هسته غیرفعالش می‌کند).
/// چکِ تردد در ماژولِ Attendance جداگانه ثبت می‌شود (هسته همهٔ چک‌کننده‌ها را با‌هم می‌بیند).
/// </summary>
public sealed class PayrollEmployeeDependencyChecker : IEmployeeDependencyChecker
{
    private readonly IRepository<SalarySlip> _slips;
    public PayrollEmployeeDependencyChecker(IRepository<SalarySlip> slips) => _slips = slips;

    public Task<bool> HasHistoryAsync(int employeeId, CancellationToken ct = default)
        => _slips.AnyAsync(s => s.EmployeeId == employeeId, ct);
}
