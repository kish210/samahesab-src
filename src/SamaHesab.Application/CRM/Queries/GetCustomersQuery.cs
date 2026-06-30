using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.CRM.Queries;

/// <summary>فهرستِ مشتریان — منبعِ واحد (API + دسکتاپ). الگوی API-only.</summary>
public record CustomerRowDto(int Id, string Code, string Name, string Mobile, decimal Balance, string PriceLevel, bool IsActive, int? BranchId = null);

/// <summary>P3 — فیلترِ اختیاریِ شعبه: اگر <paramref name="BranchId"/> داده شود فقط طرف‌حساب‌های همان شعبه
/// + مشترک‌ها (BranchId=null) برمی‌گردند؛ پیش‌فرض (null) = همهٔ شعب (سازگارِ عقب‌رو).</summary>
public record GetCustomersQuery(string? Search = null, int? BranchId = null) : IRequest<List<CustomerRowDto>>;

public class GetCustomersQueryHandler : IRequestHandler<GetCustomersQuery, List<CustomerRowDto>>
{
    private readonly IRepository<Party> _customers;
    private readonly ICurrentUserService _currentUser;
    public GetCustomersQueryHandler(IRepository<Party> customers, ICurrentUserService currentUser)
    { _customers = customers; _currentUser = currentUser; }

    public async Task<List<CustomerRowDto>> Handle(GetCustomersQuery req, CancellationToken ct)
    {
        var companyId = _currentUser.CompanyId ?? 1;
        var all = await _customers.FindAsync(c => c.CompanyId == companyId && c.IsCustomer, ct);
        var term = req.Search?.Trim();
        return all
            // P3 — فیلترِ شعبه: مالکِ همان شعبه یا مشترک (null). پیش‌فرض = همه.
            .Where(c => !req.BranchId.HasValue || c.BranchId == req.BranchId || c.BranchId == null)
            .Where(c => string.IsNullOrEmpty(term)
                || (c.FullName?.Contains(term) ?? false)
                || (c.Code?.Contains(term) ?? false)
                || (c.Mobile?.Contains(term) ?? false))
            .Select(c => new CustomerRowDto(c.Id, c.Code ?? "", c.FullName ?? "", c.Mobile ?? "",
                c.Balance, c.PriceLevel, c.IsActive, c.BranchId))
            .OrderBy(c => c.Name).ToList();
    }
}
