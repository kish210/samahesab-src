using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Treasury.Queries;

/// <summary>AUDIT-3 — ماندهٔ یک حساب بر اساسِ کد (مثلِ صندوق «1-01-001»). برای هشدارِ
/// اضافه‌برداشتِ صندوق در فرمِ دریافت/پرداخت. مثبت = بدهکار (موجودیِ صندوق).</summary>
public record GetAccountBalanceQuery(string AccountCode) : IRequest<decimal>;

public class GetAccountBalanceQueryHandler : IRequestHandler<GetAccountBalanceQuery, decimal>
{
    private readonly IAccountRepository _accounts;
    private readonly ICurrentUserService _currentUser;

    public GetAccountBalanceQueryHandler(IAccountRepository accounts, ICurrentUserService currentUser)
    { _accounts = accounts; _currentUser = currentUser; }

    public async Task<decimal> Handle(GetAccountBalanceQuery req, CancellationToken ct)
    {
        var companyId = _currentUser.CompanyId ?? 1;
        var acc = await _accounts.GetByCodeAsync(companyId, req.AccountCode, ct);
        return acc is null ? 0m : await _accounts.GetBalanceAsync(acc.Id, ct);
    }
}
