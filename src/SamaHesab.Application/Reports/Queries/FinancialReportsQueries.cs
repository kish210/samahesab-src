using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Reports.Queries;

// ── DTOs ─────────────────────────────────────────────────────────────────────
/// <summary>AccountId — U-ACCT-DRILLDOWN: امکانِ رفتن از تراز آزمایشی به دفترِ معینِ همان حساب.</summary>
public record TrialBalanceRow(string Code, string Name, decimal Debit, decimal Credit, decimal Balance, int AccountId = 0);
public record LedgerRow(string Date, string VoucherNumber, string Code, string Name,
    string? Description, decimal Debit, decimal Credit, decimal Balance);
public record PlLine(string Code, string Name, decimal Amount);
public record ProfitLossDto(decimal Revenue, decimal Expense, decimal NetProfit,
    List<PlLine> Revenues, List<PlLine> Expenses);

// ── Trial Balance ────────────────────────────────────────────────────────────
// فیلتر اختیاری بُعد: مرکز هزینه / پروژه (هستهٔ ERP — تفکیک تحلیلی).
public record GetTrialBalanceQuery(string FromDate, string ToDate,
    int? CostCenterId = null, int? ProjectId = null, int? BranchId = null) : IRequest<List<TrialBalanceRow>>;

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
        var vouchers = (await _vouchers.GetByDateRangeWithItemsAsync(companyId, req.FromDate, req.ToDate, ct))
            .Where(v => req.BranchId == null || v.BranchId == req.BranchId);

        var rows = vouchers.SelectMany(v => v.Items)
            .Where(i => (req.CostCenterId == null || i.CostCenterId == req.CostCenterId)
                     && (req.ProjectId == null || i.ProjectId == req.ProjectId))
            .GroupBy(i => i.AccountId)
            .Select(g =>
            {
                var (code, name) = accounts.TryGetValue(g.Key, out var acc) ? acc : ($"#{g.Key}", "");
                var debit = g.Sum(x => x.Debit);
                var credit = g.Sum(x => x.Credit);
                return new TrialBalanceRow(code, name, debit, credit, debit - credit, g.Key);
            })
            .OrderBy(r => r.Code)
            .ToList();
        return rows;
    }
}

// ── General / Detailed Ledger ────────────────────────────────────────────────
public record GetGeneralLedgerQuery(string FromDate, string ToDate, int? AccountId,
    int? CostCenterId = null, int? ProjectId = null, int? BranchId = null) : IRequest<List<LedgerRow>>;

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
        var vouchers = (await _vouchers.GetByDateRangeWithItemsAsync(companyId, req.FromDate, req.ToDate, ct))
            .Where(v => req.BranchId == null || v.BranchId == req.BranchId);

        var lines = vouchers
            .SelectMany(v => v.Items.Select(i => (v.VoucherDate, v.VoucherNumber, Item: i)))
            .Where(x => (req.AccountId == null || x.Item.AccountId == req.AccountId)
                     && (req.CostCenterId == null || x.Item.CostCenterId == req.CostCenterId)
                     && (req.ProjectId == null || x.Item.ProjectId == req.ProjectId))
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

// ── Balance Sheet (ترازنامه) ─────────────────────────────────────────────────
public record BalanceSheetLine(string Code, string Name, decimal Amount);
public record BalanceSheetDto(
    List<BalanceSheetLine> Assets, List<BalanceSheetLine> Liabilities, List<BalanceSheetLine> Equity,
    decimal TotalAssets, decimal TotalLiabilities, decimal TotalEquity, decimal NetProfit, bool IsBalanced);

public record GetBalanceSheetQuery(string FromDate, string ToDate, int? BranchId = null) : IRequest<BalanceSheetDto>;

public class GetBalanceSheetQueryHandler : IRequestHandler<GetBalanceSheetQuery, BalanceSheetDto>
{
    private readonly IVoucherRepository _vouchers;
    private readonly IAccountRepository _accounts;
    private readonly ICurrentUserService _currentUser;

    public GetBalanceSheetQueryHandler(IVoucherRepository v, IAccountRepository a, ICurrentUserService u)
    { _vouchers = v; _accounts = a; _currentUser = u; }

    public async Task<BalanceSheetDto> Handle(GetBalanceSheetQuery req, CancellationToken ct)
    {
        var companyId = _currentUser.CompanyId ?? 1;
        var accounts = (await _accounts.GetByCompanyAsync(companyId, ct))
            .ToDictionary(a => a.Id, a => (a.Code, a.Name));
        var vouchers = (await _vouchers.GetByDateRangeWithItemsAsync(companyId, req.FromDate, req.ToDate, ct))
            .Where(v => req.BranchId == null || v.BranchId == req.BranchId);

        var assets = new Dictionary<int, decimal>();   // group 1: debit - credit
        var liab   = new Dictionary<int, decimal>();   // group 3: credit - debit
        var equity = new Dictionary<int, decimal>();   // group 2: credit - debit
        decimal revenue = 0, expense = 0;

        foreach (var i in vouchers.SelectMany(v => v.Items))
        {
            if (!accounts.TryGetValue(i.AccountId, out var acc)) continue;
            switch (AccountClassifier.Classify(acc.Code))
            {
                case AccountCategory.Asset:     assets[i.AccountId] = assets.GetValueOrDefault(i.AccountId) + (i.Debit - i.Credit); break;
                case AccountCategory.Liability: liab[i.AccountId]   = liab.GetValueOrDefault(i.AccountId)   + (i.Credit - i.Debit); break;
                case AccountCategory.Equity:    equity[i.AccountId] = equity.GetValueOrDefault(i.AccountId) + (i.Credit - i.Debit); break;
                case AccountCategory.Revenue:   revenue += i.Credit - i.Debit; break;
                case AccountCategory.Expense:   expense += i.Debit - i.Credit; break;
            }
        }

        List<BalanceSheetLine> Lines(Dictionary<int, decimal> src) => src
            .Where(kv => kv.Value != 0)
            .Select(kv => new BalanceSheetLine(accounts[kv.Key].Code, accounts[kv.Key].Name, kv.Value))
            .OrderBy(l => l.Code).ToList();

        var netProfit = revenue - expense;
        var equityLines = Lines(equity);
        equityLines.Add(new BalanceSheetLine("—", "سود (زیان) انباشته دوره", netProfit));

        var totalAssets = assets.Values.Sum();
        var totalLiab = liab.Values.Sum();
        var totalEquity = equity.Values.Sum() + netProfit;

        return new BalanceSheetDto(Lines(assets), Lines(liab), equityLines,
            totalAssets, totalLiab, totalEquity,
            netProfit, System.Math.Abs(totalAssets - (totalLiab + totalEquity)) < 1m);
    }
}

// ── Profit & Loss ────────────────────────────────────────────────────────────
public record GetProfitLossQuery(string FromDate, string ToDate, int? BranchId = null) : IRequest<ProfitLossDto>;

public class GetProfitLossQueryHandler : IRequestHandler<GetProfitLossQuery, ProfitLossDto>
{
    private readonly IVoucherRepository _vouchers;
    private readonly IAccountRepository _accounts;
    private readonly ICurrentUserService _currentUser;

    public GetProfitLossQueryHandler(IVoucherRepository v, IAccountRepository a, ICurrentUserService u)
    { _vouchers = v; _accounts = a; _currentUser = u; }

    public async Task<ProfitLossDto> Handle(GetProfitLossQuery req, CancellationToken ct)
    {
        var companyId = _currentUser.CompanyId ?? 1;
        var accounts = (await _accounts.GetByCompanyAsync(companyId, ct))
            .ToDictionary(a => a.Id, a => (a.Code, a.Name));
        var vouchers = (await _vouchers.GetByDateRangeWithItemsAsync(companyId, req.FromDate, req.ToDate, ct))
            .Where(v => req.BranchId == null || v.BranchId == req.BranchId);

        // طبق نمودار واقعی: گروه ۶ = درآمد، گروه‌های ۷/۸/۹ = هزینه (AccountClassifier).
        var revenue = new Dictionary<int, decimal>();
        var expense = new Dictionary<int, decimal>();

        foreach (var i in vouchers.SelectMany(v => v.Items))
        {
            if (!accounts.TryGetValue(i.AccountId, out var acc)) continue;
            var cat = AccountClassifier.Classify(acc.Code);
            if (cat == AccountCategory.Revenue)
                revenue[i.AccountId] = revenue.GetValueOrDefault(i.AccountId) + (i.Credit - i.Debit);
            else if (cat == AccountCategory.Expense)
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
