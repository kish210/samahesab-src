using MediatR;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Inventory.Queries;

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
