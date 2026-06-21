using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Reports.Queries;

/// <summary>یک ردیفِ دفترِ روزنامه (هر آرتیکلِ سند، به‌ترتیبِ زمانی).</summary>
public record DaybookRow(string Date, string VoucherNumber, string AccountCode, string AccountName,
    string Description, decimal Debit, decimal Credit, int VoucherId = 0);

/// <summary>
/// فاز ۱۲ (RC-6) — دفترِ روزنامه (Daybook/Journal): فهرستِ زمانیِ همهٔ آرتیکل‌های اسنادِ یک بازه.
/// مرتب بر اساسِ تاریخ → شمارهٔ سند → ردیف.
/// </summary>
public record GetDaybookQuery(string FromDate, string ToDate) : IRequest<List<DaybookRow>>;

public class GetDaybookQueryHandler : IRequestHandler<GetDaybookQuery, List<DaybookRow>>
{
    private readonly ICurrentUserService _user;
    private readonly IVoucherRepository _vouchers;
    private readonly IAccountRepository _accounts;

    public GetDaybookQueryHandler(ICurrentUserService user, IVoucherRepository vouchers, IAccountRepository accounts)
    { _user = user; _vouchers = vouchers; _accounts = accounts; }

    public async Task<List<DaybookRow>> Handle(GetDaybookQuery req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        var vouchers = await _vouchers.GetByDateRangeWithItemsAsync(companyId, req.FromDate, req.ToDate, ct);
        var accounts = (await _accounts.GetByCompanyAsync(companyId, ct))
            .ToDictionary(a => a.Id, a => (a.Code, a.Name));

        var rows = new List<DaybookRow>();
        foreach (var v in vouchers.OrderBy(v => v.VoucherDate).ThenBy(v => v.VoucherNumber, StringComparer.Ordinal))
        {
            foreach (var it in v.Items.OrderBy(i => i.RowNumber))
            {
                var (code, name) = accounts.TryGetValue(it.AccountId, out var a) ? a : ("—", $"#{it.AccountId}");
                rows.Add(new DaybookRow(
                    v.VoucherDate, v.VoucherNumber, code, name,
                    string.IsNullOrWhiteSpace(it.Description) ? (v.Description ?? "") : it.Description!,
                    it.Debit, it.Credit, v.Id));
            }
        }
        return rows;
    }
}
