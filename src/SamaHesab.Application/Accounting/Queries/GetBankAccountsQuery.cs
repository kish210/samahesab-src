using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Accounting.Queries;

/// <summary>حساب‌های بانکی — منبعِ واحد (API + دسکتاپ). الگوی API-only.</summary>
public record BankAccountDto(int Id, string BankName, string AccountNumber, string Sheba,
    string CardNumber, string BranchName, decimal OpeningBalance, bool IsActive);

public record GetBankAccountsQuery(bool ActiveOnly = false) : IRequest<List<BankAccountDto>>;

public class GetBankAccountsQueryHandler : IRequestHandler<GetBankAccountsQuery, List<BankAccountDto>>
{
    private readonly IRepository<BankAccount> _banks;
    private readonly ICurrentUserService _currentUser;
    public GetBankAccountsQueryHandler(IRepository<BankAccount> banks, ICurrentUserService currentUser)
    { _banks = banks; _currentUser = currentUser; }

    public async Task<List<BankAccountDto>> Handle(GetBankAccountsQuery req, CancellationToken ct)
    {
        var companyId = _currentUser.CompanyId ?? 1;
        var list = await _banks.FindAsync(b => b.CompanyId == companyId, ct);
        return list
            .Where(b => !req.ActiveOnly || b.IsActive)
            .Select(b => new BankAccountDto(b.Id, b.BankName, b.AccountNumber, b.ShebaNumber ?? "",
                b.CardNumber ?? "", b.BranchName ?? "", b.OpeningBalance, b.IsActive))
            .ToList();
    }
}
