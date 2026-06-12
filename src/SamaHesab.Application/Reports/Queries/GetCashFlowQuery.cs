using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Reports.Queries;

/// <summary>
/// صورت جریان وجوه نقد (هستهٔ ERP / خزانه): تغییرِ نقد در بازه به تفکیکِ
/// عملیاتی/سرمایه‌گذاری/تأمین‌مالی + ماندهٔ اول و آخرِ دوره.
/// </summary>
public record GetCashFlowQuery(string FromDate, string ToDate, int? BranchId = null)
    : IRequest<CashFlowDto>;

public record CashFlowDto(decimal Operating, decimal Investing, decimal Financing,
    decimal NetChange, decimal OpeningCash, decimal ClosingCash);

public class GetCashFlowQueryHandler : IRequestHandler<GetCashFlowQuery, CashFlowDto>
{
    private readonly IVoucherRepository _vouchers;
    private readonly IAccountRepository _accounts;
    private readonly ICurrentUserService _currentUser;

    public GetCashFlowQueryHandler(IVoucherRepository v, IAccountRepository a, ICurrentUserService u)
    { _vouchers = v; _accounts = a; _currentUser = u; }

    public async Task<CashFlowDto> Handle(GetCashFlowQuery req, CancellationToken ct)
    {
        var companyId = _currentUser.CompanyId ?? 1;
        var codeById = (await _accounts.GetByCompanyAsync(companyId, ct))
            .ToDictionary(a => a.Id, a => a.Code);

        bool BranchOk(Domain.Entities.Accounting.Voucher v) => req.BranchId == null || v.BranchId == req.BranchId;

        // حرکت‌های نقدِ داخل بازه → موتور دسته‌بندی.
        var inRange = (await _vouchers.GetByDateRangeWithItemsAsync(companyId, req.FromDate, req.ToDate, ct))
            .Where(v => !v.IsReversed && BranchOk(v));

        var movements = new List<CashMovement>();
        foreach (var v in inRange)
        {
            decimal cashDelta = 0;
            var counterparts = new List<string>();
            foreach (var i in v.Items)
            {
                var code = codeById.TryGetValue(i.AccountId, out var c) ? c : "";
                if (CashFlowClassifier.IsCash(code)) cashDelta += i.Debit - i.Credit;
                else counterparts.Add(code);
            }
            if (cashDelta != 0) movements.Add(new CashMovement(cashDelta, counterparts));
        }

        var result = CashFlowEngine.Build(movements);

        // ماندهٔ ابتدای دوره = خالصِ همهٔ حرکت‌های نقد پیش از FromDate.
        var opening = await CashBalanceBeforeAsync(companyId, req.FromDate, req.BranchId, codeById, ct);
        var closing = opening + result.NetChange;

        return new CashFlowDto(result.Operating, result.Investing, result.Financing,
            result.NetChange, opening, closing);
    }

    private async Task<decimal> CashBalanceBeforeAsync(int companyId, string fromDate, int? branchId,
        IReadOnlyDictionary<int, string> codeById, CancellationToken ct)
    {
        // از ابتدای تاریخ تا روزِ پیش از شروع (مقایسهٔ لغویِ تاریخ شمسی).
        var prior = (await _vouchers.GetByDateRangeWithItemsAsync(companyId, "0000/00/00", fromDate, ct))
            .Where(v => !v.IsReversed
                     && string.CompareOrdinal(v.VoucherDate, fromDate) < 0
                     && (branchId == null || v.BranchId == branchId));
        decimal bal = 0;
        foreach (var i in prior.SelectMany(v => v.Items))
            if (codeById.TryGetValue(i.AccountId, out var code) && CashFlowClassifier.IsCash(code))
                bal += i.Debit - i.Credit;
        return bal;
    }
}
