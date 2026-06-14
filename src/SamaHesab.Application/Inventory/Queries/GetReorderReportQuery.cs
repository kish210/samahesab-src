using MediatR;
using SamaHesab.Application.Automation;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.Inventory;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Inventory.Queries;

/// <summary>
/// گزارشِ «کالاهای زیرِ حداقل / نقطهٔ سفارش» (reorder report) — هستهٔ ERP.
/// کالاهایی که موجودیِ کل‌شان (در همهٔ انبارها) ≤ آستانه (نقطهٔ سفارش یا حداقل) است،
/// همراه با کسری و پیشنهادِ مقدارِ سفارش. تصمیم/آستانه/پیشنهاد از <see cref="ReorderEngine"/>
/// (همان موتورِ تست‌شده) می‌آید؛ این کوئری فقط با کد/حداقل/نقطهٔ سفارش غنی‌اش می‌کند.
/// </summary>
public record GetReorderReportQuery(string? Search = null) : IRequest<ReorderReportDto>;

public record ReorderReportRow(
    int ProductId, string Code, string Name, decimal OnHand,
    decimal MinStock, decimal? ReorderPoint, decimal Threshold, decimal Shortage, decimal SuggestedQty);

public record ReorderReportDto(IReadOnlyList<ReorderReportRow> Rows, int ItemCount, decimal TotalSuggestedQty);

public class GetReorderReportQueryHandler : IRequestHandler<GetReorderReportQuery, ReorderReportDto>
{
    private readonly IRepository<Product> _products;
    private readonly IRepository<StockItem> _stock;
    private readonly ICurrentUserService _user;

    public GetReorderReportQueryHandler(IRepository<Product> products,
        IRepository<StockItem> stock, ICurrentUserService user)
    { _products = products; _stock = stock; _user = user; }

    public async Task<ReorderReportDto> Handle(GetReorderReportQuery req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        var products = await _products.FindAsync(
            p => p.CompanyId == companyId && (p.MinStock > 0 || p.ReorderPoint > 0), ct);
        var byId = products.ToDictionary(p => p.Id);
        var ids = byId.Keys.ToList();

        var onHand = (await _stock.FindAsync(s => ids.Contains(s.ProductId), ct))
            .GroupBy(s => s.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

        var inputs = products.Select(p => new ReorderInput(
            p.Id, p.Name, onHand.TryGetValue(p.Id, out var q) ? q : 0,
            p.MinStock, p.ReorderPoint, p.MaxStock));

        var rows = ReorderEngine.Suggest(inputs)            // فقط کالاهای زیرِ آستانه، مرتب بر فوریت
            .Select(s =>
            {
                var p = byId[s.ProductId];
                return new ReorderReportRow(
                    s.ProductId, p.Code, s.Name, s.OnHand,
                    p.MinStock, p.ReorderPoint, s.Threshold,
                    Shortage: s.Threshold - s.OnHand, SuggestedQty: s.SuggestedQty);
            })
            .Where(r => string.IsNullOrWhiteSpace(req.Search)
                     || r.Name.Contains(req.Search!) || r.Code.Contains(req.Search!))
            .ToList();

        return new ReorderReportDto(rows, rows.Count, rows.Sum(r => r.SuggestedQty));
    }
}
