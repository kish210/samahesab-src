using MediatR;
using SamaHesab.Application.Automation;
using SamaHesab.Application.Automation.Queries;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Domain.Enums;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.BI.Queries;

// ── Accountant Dashboard ─────────────────────────────────────────────────────
public record GetAccountantDashboardQuery(string Today) : IRequest<AccountantDashboardDto>;

public record AccountantDashboardDto(
    int DraftVouchers,
    decimal ReceivablesTotal,
    decimal PayablesTotal,
    int ChequesInProcess,
    int ChequesOverdue,
    int ChequesDueToday);

public class GetAccountantDashboardQueryHandler
    : IRequestHandler<GetAccountantDashboardQuery, AccountantDashboardDto>
{
    private readonly IRepository<Voucher> _vouchers;
    private readonly IRepository<Party> _customers;
    private readonly IRepository<Party> _suppliers;
    private readonly IChequeRepository _cheques;
    private readonly ICurrentUserService _currentUser;

    public GetAccountantDashboardQueryHandler(IRepository<Voucher> vouchers,
        IRepository<Party> customers, IRepository<Party> suppliers,
        IChequeRepository cheques, ICurrentUserService currentUser)
    { _vouchers = vouchers; _customers = customers; _suppliers = suppliers; _cheques = cheques; _currentUser = currentUser; }

    public async Task<AccountantDashboardDto> Handle(GetAccountantDashboardQuery req, CancellationToken ct)
    {
        var companyId = _currentUser.CompanyId ?? 1;

        var drafts = await _vouchers.CountAsync(
            v => v.CompanyId == companyId && v.Status == VoucherStatus.Draft, ct);
        var receivables = (await _customers.FindAsync(c => c.CompanyId == companyId && c.IsCustomer && c.Balance > 0.01m, ct))
            .Sum(c => c.Balance);
        var payables = (await _suppliers.FindAsync(s => s.CompanyId == companyId && s.IsSupplier && s.Balance > 0.01m, ct))
            .Sum(s => s.Balance);

        var inProcess = await _cheques.GetByStatusAsync(companyId, ChequeStatus.InProcess, ct);
        var overdue = inProcess.Count(c =>
            SamaHesab.Application.Accounting.ChequeBoard.Classify(c.DueDate, req.Today)
                == SamaHesab.Application.Accounting.ChequeDueState.Overdue);
        var dueToday = inProcess.Count(c =>
            SamaHesab.Application.Accounting.ChequeBoard.Classify(c.DueDate, req.Today)
                == SamaHesab.Application.Accounting.ChequeDueState.DueToday);

        return new AccountantDashboardDto(drafts, receivables, payables, inProcess.Count, overdue, dueToday);
    }
}

// ── Warehouse Dashboard ──────────────────────────────────────────────────────
public record GetWarehouseDashboardQuery(string Today) : IRequest<WarehouseDashboardDto>;

public record WarehouseDashboardDto(
    int OutOfStockCount,
    int LowStockCount,
    int ReorderSuggestions);

public class GetWarehouseDashboardQueryHandler
    : IRequestHandler<GetWarehouseDashboardQuery, WarehouseDashboardDto>
{
    private readonly IMediator _mediator;
    public GetWarehouseDashboardQueryHandler(IMediator mediator) => _mediator = mediator;

    public async Task<WarehouseDashboardDto> Handle(GetWarehouseDashboardQuery req, CancellationToken ct)
    {
        var alerts = await _mediator.Send(new GetAlertsQuery(req.Today), ct);
        var reorders = await _mediator.Send(new GetReorderSuggestionsQuery(), ct);
        return new WarehouseDashboardDto(
            alerts.Count(a => a.Kind == "OutOfStock"),
            alerts.Count(a => a.Kind == "LowStock"),
            reorders.Count);
    }
}
