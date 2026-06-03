using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Reports.Queries;

// ── DTOs ─────────────────────────────────────────────────────────────────────
public record TrialBalanceRow(string Code, string Name, decimal Debit, decimal Credit, decimal Balance);
public record LedgerRow(string Date, string VoucherNumber, string Code, string Name,
    string? Description, decimal Debit, decimal Credit, decimal Balance);
public record PlLine(string Code, string Name, decimal Amount);
public record ProfitLossDto(decimal Revenue, decimal Expense, decimal NetProfit,
    List<PlLine> Revenues, List<PlLine> Expenses);

// ── Trial Balance ────────────────────────────────────────────────────────────
public record GetTrialBalanceQuery(string FromDate, string ToDate) : IRequest<List<TrialBalanceRow>>;

public class GetTrialBalanceQueryHandler : IRequestHandler<GetTrialBalanceQuery, List<TrialBalanceRow>>
{
    private readonly IVoucherRepository _vouchers;
    private readonly IAccountRepository _accounts;
    private readonly ICurrentUserService _currentUser;

    public GetTrialBalanceQueryHandler(IVoucherRepository v, IAccountRepository a, ICurrentUserService u)
    { _vouchers = v; _accounts = a; _currentUser = u; }

    public async Task<List<TrialBalanceRow>> Handle(GetTrialBalanceQuery req, CancellationToken ct)
    {
        var companyId = _currentUser.CompanyId ?? 1;
        var accounts = (await _accounts.GetByCompanyAsync(companyId, ct))
            .ToDictionary(a => a.Id, a => (a.Code, a.Name));
        var vouchers = await _vouchers.GetByDateRangeWithItemsAsync(companyId, req.FromDate, req.ToDate, ct);

        var rows = vouchers.SelectMany(v => v.Items)
            .GroupBy(i => i.AccountId)
            .Select(g =>
            {
                var (code, name) = accounts.TryGetValue(g.Key, out var acc) ? acc : ($"#{g.Key}", "");
                var debit = g.Sum(x => x.Debit);
                var credit = g.Sum(x => x.Credit);
                return new TrialBalanceRow(code, name, debit, credit, debit - credit);
            })
            .OrderBy(r => r.Code)
            .ToList();
        return rows;
    }
}

// ── General / Detailed Ledger ────────────────────────────────────────────────
public record GetGeneralLedgerQuery(string FromDate, string ToDate, int? AccountId) : IRequest<List<LedgerRow>>;

public class GetGeneralLedgerQueryHandler : IRequestHandler<GetGeneralLedgerQuery, List<LedgerRow>>
{
    private readonly IVoucherRepository _vouchers;
    private readonly IAccountRepository _accounts;
    private readonly ICurrentUserService _currentUser;

    public GetGeneralLedgerQueryHandler(IVoucherRepository v, IAccountRepository a, ICurrentUserService u)
    { _vouchers = v; _accounts = a; _currentUser = u; }

    public async Task<List<LedgerRow>> Handle(GetGeneralLedgerQuery req, CancellationToken ct)
    {
        var companyId = _currentUser.CompanyId ?? 1;
        var accounts = (await _accounts.GetByCompanyAsync(companyId, ct))
            .ToDictionary(a => a.Id, a => (a.Code, a.Name));
        var vouchers = await _vouchers.GetByDateRangeWithItemsAsync(companyId, req.FromDate, req.ToDate, ct);

        var lines = vouchers
            .SelectMany(v => v.Items.Select(i => (v.VoucherDate, v.VoucherNumber, Item: i)))
            .Where(x => req.AccountId == null || x.Item.AccountId == req.AccountId)
            .OrderBy(x => x.VoucherDate).ThenBy(x => x.VoucherNumber)
            .ToList();

        var result = new List<LedgerRow>();
        decimal running = 0;
        foreach (var x in lines)
        {
            running += x.Item.Debit - x.Item.Credit;
            var (code, name) = accounts.TryGetValue(x.Item.AccountId, out var acc) ? acc : ($"#{x.Item.AccountId}", "");
            result.Add(new LedgerRow(x.VoucherDate, x.VoucherNumber, code, name,
                x.Item.Description, x.Item.Debit, x.Item.Credit, running));
        }
        return result;
    }
}

// ── Profit & Loss ────────────────────────────────────────────────────────────
public record GetProfitLossQuery(string FromDate, string ToDate) : IRequest<ProfitLossDto>;

public class GetProfitLossQueryHandler : IRequestHandler<GetProfitLossQuery, ProfitLossDto>
{
    private readonly IVoucherRepository _vouchers;
    private readonly IAccountRepository _accounts;
    private readonly ICurrentUserService _currentUser;

    public GetProfitLossQueryHandler(IVoucherRepository v, IAccountRepository a, ICurrentUserService u)
    { _vouchers = v; _accounts = a; _currentUser = u; }

    private static string Seg0(string code) => (code ?? "").Split('-').FirstOrDefault() ?? "";

    public async Task<ProfitLossDto> Handle(GetProfitLossQuery req, CancellationToken ct)
    {
        var companyId = _currentUser.CompanyId ?? 1;
        var accounts = (await _accounts.GetByCompanyAsync(companyId, ct))
            .ToDictionary(a => a.Id, a => (a.Code, a.Name));
        var vouchers = await _vouchers.GetByDateRangeWithItemsAsync(companyId, req.FromDate, req.ToDate, ct);

        // Iranian charts vary: treat code groups {4,6} as revenue and {5,7,8} as expense/COGS.
        var revenue = new Dictionary<int, decimal>();
        var expense = new Dictionary<int, decimal>();

        foreach (var i in vouchers.SelectMany(v => v.Items))
        {
            if (!accounts.TryGetValue(i.AccountId, out var acc)) continue;
            var g = Seg0(acc.Code);
            if (g is "4" or "6")
                revenue[i.AccountId] = revenue.GetValueOrDefault(i.AccountId) + (i.Credit - i.Debit);
            else if (g is "5" or "7" or "8")
                expense[i.AccountId] = expense.GetValueOrDefault(i.AccountId) + (i.Debit - i.Credit);
        }

        List<PlLine> Lines(Dictionary<int, decimal> src) => src
            .Select(kv => new PlLine(accounts[kv.Key].Code, accounts[kv.Key].Name, kv.Value))
            .OrderBy(l => l.Code).ToList();

        var rev = revenue.Values.Sum();
        var exp = expense.Values.Sum();
        return new ProfitLossDto(rev, exp, rev - exp, Lines(revenue), Lines(expense));
    }
}
