using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.Sales;
using SamaHesab.Domain.Enums;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.BI.Queries;

// ── Cashier Dashboard ────────────────────────────────────────────────────────
public record GetCashierDashboardQuery(string Today) : IRequest<CashierDashboardDto>;

public record CashierDashboardDto(
    decimal TodaySales, int TodayInvoiceCount, decimal AverageInvoice);

public class GetCashierDashboardQueryHandler
    : IRequestHandler<GetCashierDashboardQuery, CashierDashboardDto>
{
    private readonly IRepository<SalesInvoice> _invoices;
    private readonly ICurrentUserService _currentUser;

    public GetCashierDashboardQueryHandler(IRepository<SalesInvoice> invoices, ICurrentUserService currentUser)
    { _invoices = invoices; _currentUser = currentUser; }

    public async Task<CashierDashboardDto> Handle(GetCashierDashboardQuery req, CancellationToken ct)
    {
        var companyId = _currentUser.CompanyId ?? 1;
        var today = await SalesQueryHelper.LoadSalesAsync(_invoices, companyId, req.Today, req.Today, ct);
        var total = today.Sum(i => i.GrandTotal);
        var count = today.Count;
        var avg = count == 0 ? 0 : Math.Round(total / count, 0);
        return new CashierDashboardDto(total, count, avg);
    }
}

// GetRestaurantDashboardQuery → منتقل شد به SamaHesab.Modules.Restaurant.Application (MOD-REST).

// ── Owner Dashboard (multi-branch overview) ──────────────────────────────────
public record GetOwnerDashboardQuery(string Today) : IRequest<OwnerDashboardDto>;

public record OwnerDashboardDto(
    decimal TodaySales, decimal MonthSales, decimal MonthProfit, decimal MonthMarginPercent,
    decimal ReceivablesTotal, decimal PayablesTotal, int ChequesInProcess,
    List<BranchPerformanceDto> Branches);

public class GetOwnerDashboardQueryHandler : IRequestHandler<GetOwnerDashboardQuery, OwnerDashboardDto>
{
    private readonly IMediator _mediator;
    public GetOwnerDashboardQueryHandler(IMediator mediator) => _mediator = mediator;

    public async Task<OwnerDashboardDto> Handle(GetOwnerDashboardQuery req, CancellationToken ct)
    {
        var month = SalesAnalytics.MonthKey(req.Today);
        var mgr = await _mediator.Send(new GetManagerDashboardQuery(req.Today), ct);
        var branches = await _mediator.Send(new GetBranchPerformanceQuery(month + "/01", month + "/31"), ct);

        return new OwnerDashboardDto(
            mgr.TodaySales, mgr.MonthSales, mgr.MonthProfit, mgr.MonthMarginPercent,
            mgr.ReceivablesTotal, mgr.PayablesTotal, mgr.ChequesInProcess, branches);
    }
}
