using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Domain.Entities.Sales;
using SamaHesab.Domain.Enums;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.BI.Queries;

/// <summary>داشبورد نقش «مدیر/مالک» — KPIهای کلیدی کسب‌وکار در یک واکشی.</summary>
public record GetManagerDashboardQuery(string Today) : IRequest<ManagerDashboardDto>;

public record ManagerDashboardDto(
    decimal TodaySales,
    decimal MonthSales,
    decimal MonthProfit,
    decimal MonthMarginPercent,
    decimal ReceivablesTotal,
    decimal PayablesTotal,
    int ChequesInProcess,
    List<TopCustomerDto> TopCustomers);

public class GetManagerDashboardQueryHandler : IRequestHandler<GetManagerDashboardQuery, ManagerDashboardDto>
{
    private readonly IMediator _mediator;
    private readonly IRepository<SalesInvoice> _invoices;
    private readonly IRepository<Party> _customers;
    private readonly IRepository<Party> _suppliers;
    private readonly IChequeRepository _cheques;
    private readonly ICurrentUserService _currentUser;

    public GetManagerDashboardQueryHandler(IMediator mediator, IRepository<SalesInvoice> invoices,
        IRepository<Party> customers, IRepository<Party> suppliers,
        IChequeRepository cheques, ICurrentUserService currentUser)
    {
        _mediator = mediator; _invoices = invoices; _customers = customers;
        _suppliers = suppliers; _cheques = cheques; _currentUser = currentUser;
    }

    public async Task<ManagerDashboardDto> Handle(GetManagerDashboardQuery req, CancellationToken ct)
    {
        var companyId = _currentUser.CompanyId ?? 1;
        var month = SalesAnalytics.MonthKey(req.Today);          // yyyy/MM
        var monthStart = month + "/01";
        var monthEnd = month + "/31";

        var todaySalesList = await SalesQueryHelper.LoadSalesAsync(_invoices, companyId, req.Today, req.Today, ct);
        var todaySales = todaySalesList.Sum(i => i.GrandTotal);

        var profit = await _mediator.Send(new GetProfitAnalysisQuery(monthStart, monthEnd, 5), ct);
        var topCustomers = await _mediator.Send(new GetTopCustomersQuery(monthStart, monthEnd, 5), ct);

        var receivables = (await _customers.FindAsync(c => c.CompanyId == companyId && c.IsCustomer && c.Balance > 0.01m, ct))
            .Sum(c => c.Balance);
        var payables = (await _suppliers.FindAsync(s => s.CompanyId == companyId && s.IsSupplier && s.Balance > 0.01m, ct))
            .Sum(s => s.Balance);
        var chequesInProcess = (await _cheques.GetByStatusAsync(companyId, ChequeStatus.InProcess, ct)).Count;

        return new ManagerDashboardDto(
            todaySales, profit.Sales, profit.Profit, profit.MarginPercent,
            receivables, payables, chequesInProcess, topCustomers);
    }
}
