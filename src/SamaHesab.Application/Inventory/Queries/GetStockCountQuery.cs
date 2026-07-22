using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Inventory.Queries;

/// <summary>U-WEB-STOCKCOUNT — فهرستِ همهٔ اسنادِ انبارگردانی (پیش‌تر فقط جزئیاتِ تکی
/// (GetStockCountQuery) موجود بود، هیچ کوئریِ فهرست‌کننده‌ای نبود).</summary>
public record StockCountListItemDto(int Id, int WarehouseId, string WarehouseName, string Date, string Status);

public record GetStockCountsQuery : IRequest<List<StockCountListItemDto>>;

public class GetStockCountsQueryHandler : IRequestHandler<GetStockCountsQuery, List<StockCountListItemDto>>
{
    private readonly IStockCountRepository _sessions;
    private readonly IWarehouseRepository _warehouses;
    private readonly ICurrentUserService _user;

    public GetStockCountsQueryHandler(IStockCountRepository sessions, IWarehouseRepository warehouses, ICurrentUserService user)
    { _sessions = sessions; _warehouses = warehouses; _user = user; }

    public async Task<List<StockCountListItemDto>> Handle(GetStockCountsQuery req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        var sessions = await _sessions.FindAsync(s => s.CompanyId == companyId, ct);
        var warehouses = (await _warehouses.GetByCompanyAsync(companyId, ct)).ToDictionary(w => w.Id, w => w.Name);
        return sessions.OrderByDescending(s => s.Id)
            .Select(s => new StockCountListItemDto(s.Id, s.WarehouseId,
                warehouses.GetValueOrDefault(s.WarehouseId, $"#{s.WarehouseId}"), s.Date, s.IsPosted ? "نهایی‌شده" : "باز"))
            .ToList();
    }
}

/// <summary>جزئیات یک سند انبارگردانی با ردیف‌ها و مغایرت‌ها.</summary>
public record GetStockCountQuery(int SessionId) : IRequest<StockCountDto?>;

public record StockCountDto(int Id, int WarehouseId, string Date, string Status,
    int LineCount, int VarianceCount, List<StockCountLineDto> Lines);
public record StockCountLineDto(int ProductId, string ProductName, decimal SystemQty,
    decimal CountedQty, decimal Variance);

public class GetStockCountQueryHandler : IRequestHandler<GetStockCountQuery, StockCountDto?>
{
    private readonly IStockCountRepository _sessions;
    public GetStockCountQueryHandler(IStockCountRepository sessions) => _sessions = sessions;

    public async Task<StockCountDto?> Handle(GetStockCountQuery req, CancellationToken ct)
    {
        var s = await _sessions.GetWithLinesAsync(req.SessionId, ct);
        if (s is null) return null;
        var lines = s.Lines
            .OrderBy(l => l.ProductName)
            .Select(l => new StockCountLineDto(l.ProductId, l.ProductName, l.SystemQty, l.CountedQty, l.Variance))
            .ToList();
        return new StockCountDto(s.Id, s.WarehouseId, s.Date, s.IsPosted ? "نهایی‌شده" : "باز",
            lines.Count, lines.Count(l => l.Variance != 0), lines);
    }
}
