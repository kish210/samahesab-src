using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.CRM.Queries;

/// <summary>
/// اشخاص (طرف‌حساب) — فهرستِ یکپارچهٔ مشتری + تأمین‌کننده.
/// منبعِ واحدِ منطق: هم API (PersonsController) و هم دسکتاپ از همین کوئری می‌خوانند.
/// مرحلهٔ ۱ از ادغامِ طرف‌حساب + الگوی مرجعِ API-only.
/// </summary>
public record PersonDto(int Id, string Code, string Name, string Mobile, decimal Balance,
    string Role, bool IsCustomer, bool IsSupplier, bool IsActive);

public record GetPersonsQuery(string? Search = null, int? RoleFilter = null) : IRequest<List<PersonDto>>;

public class GetPersonsQueryHandler : IRequestHandler<GetPersonsQuery, List<PersonDto>>
{
    private readonly IRepository<Customer> _customers;
    private readonly IRepository<Supplier> _suppliers;
    private readonly ICurrentUserService _currentUser;

    public GetPersonsQueryHandler(IRepository<Customer> customers, IRepository<Supplier> suppliers,
        ICurrentUserService currentUser)
    { _customers = customers; _suppliers = suppliers; _currentUser = currentUser; }

    public async Task<List<PersonDto>> Handle(GetPersonsQuery req, CancellationToken ct)
    {
        var companyId = _currentUser.CompanyId ?? 1;
        var list = new List<PersonDto>();

        if (req.RoleFilter is null or 1)
            foreach (var c in await _customers.FindAsync(x => x.CompanyId == companyId, ct))
                list.Add(new PersonDto(c.Id, c.Code ?? "", c.FullName ?? "", c.Mobile ?? "",
                    c.Balance, "مشتری", true, false, c.IsActive));

        if (req.RoleFilter is null or 2)
            foreach (var s in await _suppliers.FindAsync(x => x.CompanyId == companyId, ct))
                list.Add(new PersonDto(s.Id, s.Code ?? "", s.FullName ?? "", s.Mobile ?? "",
                    s.Balance, "تأمین‌کننده", false, true, s.IsActive));

        var term = req.Search?.Trim();
        if (!string.IsNullOrEmpty(term))
            list = list.Where(p => p.Name.Contains(term) || p.Code.Contains(term) || p.Mobile.Contains(term)).ToList();

        return list.OrderBy(p => p.Name).ToList();
    }
}
