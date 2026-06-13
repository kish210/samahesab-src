using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Accounting.Queries;

public record AccountDto(
    int Id, string Code, string Name, int Level, string Nature,
    string? AccountType, int? ParentId, bool IsLeaf, bool IsActive);

/// <summary>فهرستِ حساب‌های نمودار حساب‌ها (هستهٔ ERP) — برای کلاینت‌های API (وب/موبایل/دسکتاپ).</summary>
public record GetAccountsQuery(bool LeafOnly = false) : IRequest<List<AccountDto>>;

public class GetAccountsQueryHandler : IRequestHandler<GetAccountsQuery, List<AccountDto>>
{
    private readonly IAccountRepository _accounts;
    private readonly ICurrentUserService _user;

    public GetAccountsQueryHandler(IAccountRepository accounts, ICurrentUserService user)
    { _accounts = accounts; _user = user; }

    public async Task<List<AccountDto>> Handle(GetAccountsQuery req, CancellationToken ct)
    {
        var all = await _accounts.GetByCompanyAsync(_user.CompanyId ?? 1, ct);
        return all
            .Where(a => !req.LeafOnly || a.IsLeaf)
            .OrderBy(a => a.Code, StringComparer.Ordinal)
            .Select(a => new AccountDto(
                a.Id, a.Code, a.Name, (int)a.Level, a.Nature.ToString(),
                a.AccountType, a.ParentId, a.IsLeaf, a.IsActive))
            .ToList();
    }
}
