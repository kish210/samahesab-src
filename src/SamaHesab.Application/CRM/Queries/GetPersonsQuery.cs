using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.CRM.Queries;

/// <summary>
/// اشخاص (طرف‌حساب) — فهرستِ یکپارچه از جدولِ Crm.Parties (deduped، چندنقشه: مشتری/تأمین‌کننده/کارمند).
/// ماندهٔ زنده از منابعِ اصلی (Customer/Supplier) خوانده می‌شود تا کهنه نشود.
/// منبعِ واحدِ منطق: API (PersonsController) + دسکتاپ.
/// </summary>
public record PersonDto(int Id, string Code, string Name, string Mobile, decimal Balance,
    string Role, bool IsCustomer, bool IsSupplier, bool IsActive, bool IsEmployee = false);

public record GetPersonsQuery(string? Search = null, int? RoleFilter = null) : IRequest<List<PersonDto>>;

public class GetPersonsQueryHandler : IRequestHandler<GetPersonsQuery, List<PersonDto>>
{
    private readonly IRepository<Party> _parties;
    private readonly IRepository<Customer> _customers;
    private readonly IRepository<Supplier> _suppliers;
    private readonly ICurrentUserService _currentUser;

    public GetPersonsQueryHandler(IRepository<Party> parties, IRepository<Customer> customers,
        IRepository<Supplier> suppliers, ICurrentUserService currentUser)
    { _parties = parties; _customers = customers; _suppliers = suppliers; _currentUser = currentUser; }

    private static string RoleText(bool c, bool s, bool e)
    {
        var roles = new List<string>(3);
        if (c) roles.Add("مشتری");
        if (s) roles.Add("تأمین‌کننده");
        if (e) roles.Add("کارمند");
        return roles.Count > 0 ? string.Join("/", roles) : "—";
    }

    public async Task<List<PersonDto>> Handle(GetPersonsQuery req, CancellationToken ct)
    {
        var companyId = _currentUser.CompanyId ?? 1;
        var parties = await _parties.FindAsync(p => p.CompanyId == companyId, ct);

        // ماندهٔ زنده از منابعِ اصلی (با LegacyId) تا کهنه نشود.
        var custBal = (await _customers.FindAsync(c => c.CompanyId == companyId, ct)).ToDictionary(c => c.Id, c => c.Balance);
        var suppBal = (await _suppliers.FindAsync(s => s.CompanyId == companyId, ct)).ToDictionary(s => s.Id, s => s.Balance);

        IEnumerable<Party> q = parties;
        if (req.RoleFilter == 1) q = q.Where(p => p.IsCustomer);
        else if (req.RoleFilter == 2) q = q.Where(p => p.IsSupplier);
        else if (req.RoleFilter == 3) q = q.Where(p => p.IsEmployee);

        var term = req.Search?.Trim();
        var list = new List<PersonDto>();
        foreach (var p in q)
        {
            decimal bal = 0;
            if (p.LegacyCustomerId is int lc && custBal.TryGetValue(lc, out var cb)) bal += cb;
            if (p.LegacySupplierId is int ls && suppBal.TryGetValue(ls, out var sb)) bal += sb;

            var name = p.FullName;
            if (!string.IsNullOrEmpty(term) && !(name.Contains(term) || (p.Code ?? "").Contains(term) || (p.Mobile ?? "").Contains(term)))
                continue;

            list.Add(new PersonDto(p.Id, p.Code ?? "", name, p.Mobile ?? "", bal,
                RoleText(p.IsCustomer, p.IsSupplier, p.IsEmployee), p.IsCustomer, p.IsSupplier, p.IsActive, p.IsEmployee));
        }
        return list.OrderBy(p => p.Name).ToList();
    }
}
