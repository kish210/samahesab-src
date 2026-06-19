using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.CRM.Queries;

/// <summary>فهرستِ تأمین‌کنندگان — منبعِ واحد (API + دسکتاپ). الگوی API-only.</summary>
public record SupplierRowDto(int Id, string Code, string Name, string Mobile, string City, decimal Balance, bool IsActive);

public record GetSuppliersQuery(string? Search = null) : IRequest<List<SupplierRowDto>>;

public class GetSuppliersQueryHandler : IRequestHandler<GetSuppliersQuery, List<SupplierRowDto>>
{
    private readonly IRepository<Party> _suppliers;
    private readonly ICurrentUserService _currentUser;
    public GetSuppliersQueryHandler(IRepository<Party> suppliers, ICurrentUserService currentUser)
    { _suppliers = suppliers; _currentUser = currentUser; }

    public async Task<List<SupplierRowDto>> Handle(GetSuppliersQuery req, CancellationToken ct)
    {
        var companyId = _currentUser.CompanyId ?? 1;
        var all = await _suppliers.FindAsync(s => s.CompanyId == companyId && s.IsSupplier, ct);
        var term = req.Search?.Trim();
        return all
            .Where(s => string.IsNullOrEmpty(term)
                || (s.FullName?.Contains(term) ?? false)
                || (s.Code?.Contains(term) ?? false)
                || (s.Mobile?.Contains(term) ?? false))
            .Select(s => new SupplierRowDto(s.Id, s.Code ?? "", s.FullName ?? "", s.Mobile ?? "",
                s.City ?? "", s.Balance, s.IsActive))
            .OrderBy(s => s.Name).ToList();
    }
}
