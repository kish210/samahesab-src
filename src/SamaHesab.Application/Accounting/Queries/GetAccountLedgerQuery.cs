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
    private const string MinDate = "1300/01/01";
    private readonly IVoucherRepository _vouchers;
    private readonly ICurrentUserService _user;

    public GetAccountLedgerQueryHandler(IVoucherRepository vouchers, ICurrentUserService user)
    { _vouchers = vouchers; _user = user; }

    public async Task<AccountLedgerResult> Handle(GetAccountLedgerQuery req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;

        // یک واکشی از ابتدای زمان تا انتهای دوره؛ سپس تفکیکِ «پیش از دوره» (ماندهٔ ابتدا) و «در دوره».
        var vouchers = await _vouchers.GetByDateRangeWithItemsAsync(companyId, MinDate, req.ToDate, ct);

        decimal opening = 0;
        var inRange = new List<AccountLedgerRawLine>();

        foreach (var v in vouchers)
        {
            if (v.IsReversed) continue;
            foreach (var i in v.Items)
            {
                if (i.AccountId != req.AccountId) continue;

                // مقایسهٔ لغویِ تاریخِ شمسیِ yyyy/MM/dd = مقایسهٔ زمانی.
                if (string.CompareOrdinal(v.VoucherDate, req.FromDate) < 0)
                {
                    opening += i.Debit - i.Credit;   // پیش از دوره → ماندهٔ ابتدا
                }
                else
                {
                    inRange.Add(new AccountLedgerRawLine(v.Id, v.VoucherNumber, v.VoucherDate,
                        i.Debit, i.Credit,
                        string.IsNullOrWhiteSpace(i.Description) ? v.Description ?? "" : i.Description!));
                }
            }
        }

        return AccountLedger.Build(opening, inRange);
    }
}
