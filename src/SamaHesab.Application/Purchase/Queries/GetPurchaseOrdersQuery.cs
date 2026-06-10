using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.Purchase;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Purchase.Queries;

/// <summary>فهرست سفارش‌های خرید (اختیاری: فیلتر وضعیت).</summary>
public record GetPurchaseOrdersQuery(string? StatusCode = null) : IRequest<List<PurchaseOrderDto>>;

public record PurchaseOrderDto(
    int Id, string OrderNumber, string OrderDate, int? SupplierId,
    string StatusCode, string Source, decimal Total, int ItemCount);

public class GetPurchaseOrdersQueryHandler : IRequestHandler<GetPurchaseOrdersQuery, List<PurchaseOrderDto>>
{
    private readonly IRepository<PurchaseOrder> _orders;
    private readonly ICurrentUserService _currentUser;

    public GetPurchaseOrdersQueryHandler(IRepository<PurchaseOrder> orders, ICurrentUserService currentUser)
    { _orders = orders; _currentUser = currentUser; }

    public async Task<List<PurchaseOrderDto>> Handle(GetPurchaseOrdersQuery req, CancellationToken ct)
    {
        var companyId = _currentUser.CompanyId ?? 1;
        var list = await _orders.FindAsync(
            o => o.CompanyId == companyId && (req.StatusCode == null || o.StatusCode == req.StatusCode), ct);

        return list
            .OrderByDescending(o => o.Id)
            .Select(o => new PurchaseOrderDto(o.Id, o.OrderNumber, o.OrderDate, o.SupplierId,
                o.StatusCode, o.Source, o.Total, o.Items.Count))
            .ToList();
    }
}
