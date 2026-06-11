using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.Inventory;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Inventory.Queries;

/// <summary>
/// کار #۲۹ — ارزش‌گذاری FIFO یک کالا از روی کاردکس موجود (StockTransactions).
/// افزایشی است و مسیر فروش/انبار را تغییر نمی‌دهد؛ صرفاً بهای تمام‌شده‌ی FIFO و ارزش باقی‌مانده را گزارش می‌کند.
/// </summary>
public record GetFifoValuationQuery(int ProductId, int? WarehouseId = null) : IRequest<FifoValuationDto>;

public record FifoLayerDto(decimal Qty, decimal UnitCost);
public record FifoValuationDto(int ProductId, int? WarehouseId, decimal RemainingQty,
    decimal FifoValue, decimal IssuedCost, List<FifoLayerDto> Layers);

public class GetFifoValuationQueryHandler : IRequestHandler<GetFifoValuationQuery, FifoValuationDto>
{
    private readonly IRepository<StockTransaction> _ledger;
    private readonly ICurrentUserService _user;
    public GetFifoValuationQueryHandler(IRepository<StockTransaction> ledger, ICurrentUserService user)
    { _ledger = ledger; _user = user; }

    public async Task<FifoValuationDto> Handle(GetFifoValuationQuery req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        var txns = await _ledger.FindAsync(t => t.CompanyId == companyId && t.ProductId == req.ProductId
            && (req.WarehouseId == null || t.WarehouseId == req.WarehouseId), ct);

        var movements = txns.OrderBy(t => t.CreatedAt).ThenBy(t => t.Id)
            .Select(t => (t.Quantity, t.UnitCost));
        var v = FifoCostEngine.Compute(movements);

        return new FifoValuationDto(req.ProductId, req.WarehouseId, v.RemainingQty, v.RemainingValue,
            v.IssuedCost, v.RemainingLayers.Select(l => new FifoLayerDto(l.Qty, l.UnitCost)).ToList());
    }
}
