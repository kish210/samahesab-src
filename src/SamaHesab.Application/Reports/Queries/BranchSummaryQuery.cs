using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Domain.Entities.HRM;
using SamaHesab.Domain.Entities.Settings;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Reports.Queries;

/// <summary>
/// U-BRANCH-BASEDATA (@2026-07-16) — گزارشِ per-branch: تعدادِ مشتریان/تأمین‌کنندگان/کالاها/
/// انبارها/کارمندانِ اختصاصیِ هر شعبه + یک ردیفِ «مشترکِ همهٔ شعب» برایِ دادهٔ بدونِ BranchId.
/// </summary>
public record BranchSummaryRow(int? BranchId, string BranchName,
    int CustomerCount, int SupplierCount, int ProductCount, int WarehouseCount, int EmployeeCount);

public record GetBranchSummaryQuery : IRequest<List<BranchSummaryRow>>;

public class GetBranchSummaryQueryHandler : IRequestHandler<GetBranchSummaryQuery, List<BranchSummaryRow>>
{
    private readonly IRepository<Branch> _branches;
    private readonly IRepository<Party> _parties;
    private readonly IProductRepository _products;
    private readonly IWarehouseRepository _warehouses;
    private readonly IRepository<Employee> _employees;
    private readonly ICurrentUserService _user;

    public GetBranchSummaryQueryHandler(IRepository<Branch> branches, IRepository<Party> parties,
        IProductRepository products, IWarehouseRepository warehouses, IRepository<Employee> employees,
        ICurrentUserService user)
    {
        _branches = branches; _parties = parties; _products = products;
        _warehouses = warehouses; _employees = employees; _user = user;
    }

    public async Task<List<BranchSummaryRow>> Handle(GetBranchSummaryQuery req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        var branches = await _branches.FindAsync(b => b.CompanyId == companyId, ct);
        var parties = await _parties.FindAsync(p => p.CompanyId == companyId, ct);
        var products = await _products.SearchAsync(companyId, "", ct);
        var warehouses = await _warehouses.GetByCompanyAsync(companyId, ct);
        var employees = await _employees.FindAsync(e => e.CompanyId == companyId, ct);

        BranchSummaryRow Summarize(int? branchId, string name) => new(
            branchId, name,
            parties.Count(p => p.BranchId == branchId && p.IsCustomer),
            parties.Count(p => p.BranchId == branchId && p.IsSupplier),
            products.Count(p => p.BranchId == branchId),
            warehouses.Count(w => w.BranchId == branchId),
            employees.Count(e => e.BranchId == branchId));

        var rows = branches.OrderBy(b => b.Code)
            .Select(b => Summarize(b.Id, $"{b.Code} - {b.Name}"))
            .ToList();
        rows.Add(Summarize(null, "مشترکِ همهٔ شعب"));
        return rows;
    }
}
