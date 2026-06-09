using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Accounting.Queries;

/// <summary>
/// Type-ahead جست‌وجوی حساب برای ورود سریع سند: کاربر کد یا بخشی از نام را تایپ می‌کند
/// و نتیجه را با کیبورد انتخاب می‌کند (بدون نیاز به دانستن شناسه‌ی حساب).
/// فقط حساب‌های «برگ» و فعال برمی‌گردند، چون فقط آن‌ها قابل ثبت در ردیف سند هستند.
/// </summary>
public record SearchAccountsQuery(string Term, int MaxResults = 15) : IRequest<List<AccountLookupDto>>;

/// <summary>سطر سبک نتیجه‌ی جست‌وجو — همان چیزی که در سلول گرید نشان داده می‌شود.</summary>
public record AccountLookupDto(int Id, string Code, string Name, string Nature);

public class SearchAccountsQueryHandler : IRequestHandler<SearchAccountsQuery, List<AccountLookupDto>>
{
    private readonly IAccountRepository _accountRepository;
    private readonly ICurrentUserService _currentUser;

    public SearchAccountsQueryHandler(IAccountRepository accountRepository, ICurrentUserService currentUser)
    {
        _accountRepository = accountRepository;
        _currentUser = currentUser;
    }

    public async Task<List<AccountLookupDto>> Handle(SearchAccountsQuery request, CancellationToken ct)
    {
        var companyId = _currentUser.CompanyId!.Value;
        var leaves = await _accountRepository.GetLeafAccountsAsync(companyId, ct);

        var active = leaves.Where(a => a.IsActive);
        var term = request.Term?.Trim() ?? string.Empty;

        IEnumerable<Domain.Entities.Accounting.Account> matched = term.Length == 0
            ? active
            : active.Where(a => a.Code.Contains(term, StringComparison.OrdinalIgnoreCase)
                             || a.Name.Contains(term, StringComparison.OrdinalIgnoreCase));

        return matched
            // تطابق ابتدای کد، سپس ابتدای نام، در صدر فهرست تا انتخاب کیبوردی سریع‌تر شود
            .OrderByDescending(a => a.Code.StartsWith(term, StringComparison.OrdinalIgnoreCase))
            .ThenByDescending(a => a.Name.StartsWith(term, StringComparison.OrdinalIgnoreCase))
            .ThenBy(a => a.Code)
            .Take(request.MaxResults)
            .Select(a => new AccountLookupDto(a.Id, a.Code, a.Name, a.Nature.ToString()))
            .ToList();
    }
}
