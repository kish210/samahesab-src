using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Domain.Entities.HRM;
using SamaHesab.Modules.Tourism.Domain;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Modules.Tourism.Application;

/// <summary>
/// TUR-C1-6 — پلِ پورسانت→حقوق: جمعِ پورسانتِ ماهِ شمسی per-فروشنده و نگاشتِ Party→Employee (با کدِ ملی).
/// خروجی به‌عنوانِ «پورسانت فروش» در فیشِ حقوق تزریق می‌شود تا بیمه/مالیات/خالص درست محاسبه شوند.
/// </summary>
public static class CommissionPayrollBridge
{
    /// <summary>جمعِ پورسانتِ ماه به‌تفکیکِ کارمند (EmployeeId → مبلغ). فروشنده‌ای که کارمندِ متناظر ندارد حذف می‌شود.</summary>
    public static async Task<Dictionary<int, decimal>> ByEmployeeAsync(
        IRepository<SalesCommissionEntry> commissions, IRepository<Party> parties,
        IReadOnlyList<Employee> employees, int companyId, string persianYearMonth, CancellationToken ct)
    {
        var byParty = (await commissions.FindAsync(
                c => c.CompanyId == companyId && c.PersianYearMonth == persianYearMonth, ct))
            .GroupBy(c => c.SalespersonPartyId)
            .ToDictionary(g => g.Key, g => g.Sum(c => c.CommissionAmount));
        if (byParty.Count == 0) return new();

        var partyById = (await parties.FindAsync(p => p.CompanyId == companyId, ct)).ToDictionary(p => p.Id);
        var empByNat = employees
            .Where(e => !string.IsNullOrWhiteSpace(e.NationalCode))
            .GroupBy(e => e.NationalCode).ToDictionary(g => g.Key, g => g.First().Id);

        var result = new Dictionary<int, decimal>();
        foreach (var (partyId, amount) in byParty)
            if (partyById.TryGetValue(partyId, out var p) && !string.IsNullOrWhiteSpace(p.NationalCode)
                && empByNat.TryGetValue(p.NationalCode!, out var empId))
                result[empId] = result.GetValueOrDefault(empId) + amount;
        return result;
    }
}

/// <summary>گزارش/نمایشِ پورسانتِ ماهانهٔ فروشنده‌ها به‌تفکیکِ کارمند (برای صفحهٔ حقوق/گزارش).</summary>
public record GetMonthlyCommissionByEmployeeQuery(string PersianYearMonth)
    : IRequest<List<EmployeeCommissionDto>>;

public record EmployeeCommissionDto(int EmployeeId, string EmployeeName, decimal Commission);

public class GetMonthlyCommissionByEmployeeQueryHandler
    : IRequestHandler<GetMonthlyCommissionByEmployeeQuery, List<EmployeeCommissionDto>>
{
    private readonly IRepository<SalesCommissionEntry> _commissions;
    private readonly IRepository<Party> _parties;
    private readonly IRepository<Employee> _employees;
    private readonly ICurrentUserService _user;

    public GetMonthlyCommissionByEmployeeQueryHandler(IRepository<SalesCommissionEntry> commissions,
        IRepository<Party> parties, IRepository<Employee> employees, ICurrentUserService user)
    { _commissions = commissions; _parties = parties; _employees = employees; _user = user; }

    public async Task<List<EmployeeCommissionDto>> Handle(GetMonthlyCommissionByEmployeeQuery req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        var emps = await _employees.FindAsync(e => e.CompanyId == companyId, ct);
        var byEmp = await CommissionPayrollBridge.ByEmployeeAsync(_commissions, _parties, emps, companyId, req.PersianYearMonth, ct);
        var names = emps.ToDictionary(e => e.Id, e => e.FullName);
        return byEmp.Select(kv => new EmployeeCommissionDto(kv.Key, names.GetValueOrDefault(kv.Key, $"#{kv.Key}"), kv.Value))
            .OrderByDescending(x => x.Commission).ToList();
    }
}
