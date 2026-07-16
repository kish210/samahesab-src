using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Accounting.Queries;

/// <summary>
/// دفترِ معینِ یک حساب در بازهٔ تاریخ + ماندهٔ ابتدای دوره — پلِ drill-down از تراز/دفترکل به سند.
/// ماندهٔ ابتدا = جمعِ خالصِ ردیف‌های همان حساب پیش از FromDate (یک واکشیِ واحد تا ToDate).
/// </summary>
public record GetAccountLedgerQuery(int AccountId, string FromDate, string ToDate)
    : IRequest<AccountLedgerResult>;

public class GetAccountLedgerQueryHandler : IRequestHandler<GetAccountLedgerQuery, AccountLedgerResult>
{
    private readonly IVoucherRepository _vouchers;
    private readonly ICurrentUserService _user;

    public GetAccountLedgerQueryHandler(IVoucherRepository vouchers, ICurrentUserService user)
    { _vouchers = vouchers; _user = user; }

    /// <summary>
    /// U-DB-PAGING (@2026-07-16) — پیش‌تر کلِ تاریخچه از ۱۳۰۰/۰۱/۰۱ تا ToDate بارگذاری می‌شد تا
    /// ماندهٔ ابتدا در حافظه جمع بسته شود؛ حالا ماندهٔ ابتدا یک SUMِ DB-level است و فقط ردیف‌هایِ
    /// خودِ بازه (نه کلِ تاریخچه) واکشی می‌شوند.
    /// </summary>
    public async Task<AccountLedgerResult> Handle(GetAccountLedgerQuery req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;

        var opening = await _vouchers.SumAccountMovementBeforeAsync(companyId, req.AccountId, req.FromDate, ct);
        var items = await _vouchers.GetAccountItemsInRangeAsync(companyId, req.AccountId, req.FromDate, req.ToDate, ct);

        var inRange = items.Select(i => new AccountLedgerRawLine(
            i.Voucher!.Id, i.Voucher.VoucherNumber, i.Voucher.VoucherDate,
            i.Debit, i.Credit,
            string.IsNullOrWhiteSpace(i.Description) ? i.Voucher.Description ?? "" : i.Description!)).ToList();

        return AccountLedger.Build(opening, inRange);
    }
}
