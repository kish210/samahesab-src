using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.HRM;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.HRM;

/// <summary>
/// M7 — برگهٔ حضوروغیابِ یک روز: همهٔ کارکنانِ فعال + رکوردِ حضورشان در آن روز (در صورتِ وجود).
/// کارکنانِ بدونِ رکورد با وضعیتِ «ثبت‌نشده» نمایش داده می‌شوند (به‌جای دادهٔ نمونه).
/// </summary>
public record GetAttendanceQuery(string WorkDate) : IRequest<List<AttendanceDto>>;

public record AttendanceDto(int EmployeeId, string EmployeeName, string CheckIn, string CheckOut,
    decimal WorkHours, decimal OvertimeHours, string Status);

public class GetAttendanceQueryHandler : IRequestHandler<GetAttendanceQuery, List<AttendanceDto>>
{
    private readonly IRepository<Employee> _employees;
    private readonly IRepository<AttendanceRecord> _attendance;
    private readonly ICurrentUserService _user;

    public GetAttendanceQueryHandler(IRepository<Employee> employees,
        IRepository<AttendanceRecord> attendance, ICurrentUserService user)
    { _employees = employees; _attendance = attendance; _user = user; }

    public async Task<List<AttendanceDto>> Handle(GetAttendanceQuery req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        var emps = await _employees.FindAsync(e => e.CompanyId == companyId && e.IsActive, ct);
        var ids = emps.Select(e => e.Id).ToHashSet();
        var recs = (await _attendance.FindAsync(a => a.WorkDate == req.WorkDate, ct))
            .Where(a => ids.Contains(a.EmployeeId))
            .ToDictionary(a => a.EmployeeId);

        return emps
            .OrderBy(e => e.LastName)
            .Select(e =>
            {
                if (recs.TryGetValue(e.Id, out var r))
                    return new AttendanceDto(e.Id, e.FullName,
                        r.CheckIn?.ToString("HH:mm") ?? "", r.CheckOut?.ToString("HH:mm") ?? "",
                        r.WorkHours ?? 0, r.OvertimeHours ?? 0, r.Status);
                return new AttendanceDto(e.Id, e.FullName, "", "", 0, 0, "ثبت‌نشده");
            })
            .ToList();
    }
}
