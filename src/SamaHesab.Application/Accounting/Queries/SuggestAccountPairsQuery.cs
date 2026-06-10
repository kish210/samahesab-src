using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Accounting.Queries;

/// <summary>
/// وقتی کاربر در ردیف سند حسابی را انتخاب می‌کند، این Query حساب‌های طرفِ مقابلِ
/// پرتکرار (از تاریخچه) را پیشنهاد می‌دهد تا ردیف بعد با یک کلید پر شود.
/// </summary>
public record SuggestAccountPairsQuery(int AccountId, int Top = 6) : IRequest<List<AccountSuggestionDto>>;

public record AccountSuggestionDto(int Id, string Code, string Name, int Count);

public class SuggestAccountPairsQueryHandler : IRequestHandler<SuggestAccountPairsQuery, List<AccountSuggestionDto>>
{
    private readonly IVoucherRepository _vouchers;
    private readonly IAccountRepository _accounts;
    private readonly ICurrentUserService _currentUser;

    public SuggestAccountPairsQueryHandler(IVoucherRepository vouchers, IAccountRepository accounts,
        ICurrentUserService currentUser)
    { _vouchers = vouchers; _accounts = accounts; _currentUser = currentUser; }

    public async Task<List<AccountSuggestionDto>> Handle(SuggestAccountPairsQuery req, CancellationToken ct)
    {
        var companyId = _currentUser.CompanyId ?? 1;
        var accounts = (await _accounts.GetByCompanyAsync(companyId, ct))
            .ToDictionary(a => a.Id, a => (a.Code, a.Name));

        var history = await _vouchers.GetByDateRangeWithItemsAsync(companyId, "1390/01/01", "1499/12/29", ct);
        var grouped = history.Select(v =>
            (IReadOnlyCollection<int>)v.Items.Select(i => i.AccountId).Distinct().ToList());

        return AccountPairing.Suggest(grouped, req.AccountId, req.Top)
            .Where(s => accounts.ContainsKey(s.AccountId))
            .Select(s => new AccountSuggestionDto(s.AccountId, accounts[s.AccountId].Code,
                accounts[s.AccountId].Name, s.Count))
            .ToList();
    }
}
