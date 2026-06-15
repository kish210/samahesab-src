using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.HRM;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.HRM;

/// <summary>
/// M7 — فیش‌های حقوقیِ یک ماه: برای هر کارمندِ فعال، حقوق از `PayrollCalculator` محاسبه می‌شود
/// (به‌جای دادهٔ نمونه). فعلاً بر مبنای حقوقِ پایه (اضافه‌کاری/مزایا = ۰ تا اتصالِ حضوروغیاب).
/// </summary>
public record GetSalarySlipsQuery(string Year, int Month) : IRequest<List<SalarySlipDto>>;

public record SalarySlipDto(
    int EmployeeId, string EmployeeName, string Department,
    decimal BaseSalary, decimal Overtime, decimal Allowances,
    decimal Insurance, decimal Tax, decimal Net);

public class GetSalarySlipsQueryHandler : IRequestHandler<GetSalarySlipsQuery, List<SalarySlipDto>>
{
    private readonly IRepository<Employee> _employees;
    private readonly IRepository<Department> _departments;
    private readonly ICurrentUserService _user;

    public GetSalarySlipsQueryHandler(IRepository<Employee> employees,
        IRepository<Department> departments, ICurrentUserService user)
    { _employees = employees; _departments = departments; _user = user; }

    public async Task<List<SalarySlipDto>> Handle(GetSalarySlipsQuery req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        var emps = await _employees.FindAsync(e => e.CompanyId == companyId && e.IsActive, ct);
        var depts = (await _departments.FindAsync(d => d.CompanyId == companyId, ct))
            .ToDictionary(d => d.Id, d => d.Name);

        return emps
            .OrderBy(e => e.LastName)
            .Select(e =>
            {
                var r = PayrollCalculator.Compute(new PayrollInput(e.BaseSalary));
                var dept = e.DepartmentId is int did && depts.TryGetValue(did, out var n) ? n : "—";
                return new SalarySlipDto(e.Id, e.FullName, dept,
                    e.BaseSalary, 0, 0, r.Insurance, r.Tax, r.Net);
            })
            .ToList();
    }
}
