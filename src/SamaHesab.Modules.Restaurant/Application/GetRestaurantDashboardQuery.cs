using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Enums;
using SamaHesab.Domain.Interfaces.Repositories;
using SamaHesab.Modules.Restaurant.Domain;

namespace SamaHesab.Modules.Restaurant.Application;

// ── داشبوردِ رستوران (منتقل‌شده از Application/BI به ماژول، MOD-REST) ──
public record GetRestaurantDashboardQuery() : IRequest<RestaurantDashboardDto>;

public record RestaurantDashboardDto(
    int FreeTables, int OccupiedTables, int ReservedTables, int BillingTables,
    int OpenOrders, decimal OpenOrdersValue);

public class GetRestaurantDashboardQueryHandler
    : IRequestHandler<GetRestaurantDashboardQuery, RestaurantDashboardDto>
{
    private readonly IRepository<DiningTable> _tables;
    private readonly IRepository<RestaurantOrder> _orders;
    private readonly ICurrentUserService _currentUser;

    public GetRestaurantDashboardQueryHandler(IRepository<DiningTable> tables,
        IRepository<RestaurantOrder> orders, ICurrentUserService currentUser)
    { _tables = tables; _orders = orders; _currentUser = currentUser; }

    public async Task<RestaurantDashboardDto> Handle(GetRestaurantDashboardQuery req, CancellationToken ct)
    {
        var companyId = _currentUser.CompanyId ?? 1;
        var tables = await _tables.FindAsync(t => t.CompanyId == companyId, ct);
        var open = await _orders.FindAsync(
            o => o.CompanyId == companyId && o.Status != RestaurantOrderStatus.Settled
                 && o.Status != RestaurantOrderStatus.Cancelled, ct);

        return new RestaurantDashboardDto(
            tables.Count(t => t.Status == TableStatus.Free),
            tables.Count(t => t.Status == TableStatus.Occupied),
            tables.Count(t => t.Status == TableStatus.Reserved),
            tables.Count(t => t.Status == TableStatus.Billing),
            open.Count,
            open.Sum(o => o.GrandTotal));
    }
}
