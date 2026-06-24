using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.HRM;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.HRM;

/// <summary>
/// ATTP-C2-2 — فهرستِ درخواست‌های مرخصی (برای فرمِ مدیریت/تأیید در اپلیکیشنِ تردد).
/// <paramref name="PendingOnly"/>: فقط «در انتظار».
/// </summary>
public record GetLeaveRequestsQuery(bool PendingOnly = false) : IRequest<List<LeaveRequestDto>>;

public record LeaveRequestDto(
    int Id, int EmployeeId, string EmployeeName, string LeaveType,
    string StartDate, string EndDate, decimal Days, decimal Hours, string Status, string? Reason);

public class GetLeaveRequestsQueryHandler : IRequestHandler<GetLeaveRequestsQuery, List<LeaveRequestDto>>
{
    private readonly IRepository<LeaveRequest> _leaves;
    private readonly IRepository<Employee> _employees;
    private readonly ICurrentUserService _user;

    public GetLeaveRequestsQueryHandler(IRepository<LeaveRequest> leaves, IRepository<Employee> employees,
        ICurrentUserService user)
    { _leaves = leaves; _employees = employees; _user = user; }

    public async Task<List<LeaveRequestDto>> Handle(GetLeaveRequestsQuery req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        var items = await _leaves.FindAsync(
            l => l.CompanyId == companyId && (!req.PendingOnly || l.Status == LeaveRequest.StatusPending), ct);

        var empIds = items.Select(l => l.EmployeeId).ToHashSet();
        var names = (await _employees.FindAsync(e => empIds.Contains(e.Id), ct))
            .ToDictionary(e => e.Id, e => e.FullName);

        return items
            .OrderByDescending(l => l.Id)
            .Select(l => new LeaveRequestDto(
                l.Id, l.EmployeeId, names.GetValueOrDefault(l.EmployeeId, $"#{l.EmployeeId}"),
                l.LeaveType, l.StartDate, l.EndDate, l.Days, l.Hours, l.Status, l.Reason))
            .ToList();
    }
}
