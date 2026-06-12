using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Inventory.Queries;

/// <summary>
/// گزارش ارزش‌گذاری انبار (هستهٔ ERP): موجودی و ارزشِ هر کالا به‌میانگین موزون.
/// اگر <paramref name="WarehouseId"/> داده نشود، روی همهٔ انبارهای شرکت تجمیع می‌شود.
/// </summary>
public record GetInventoryValuationQuery(int? WarehouseId = null, string? Search = null)
    : IRequest<InventoryValuationDto>;

public record InventoryValuationDto(IReadOnlyList<StockRow> Rows, decimal TotalValue, int ItemCount);

public class GetInventoryValuationQueryHandler : IRequestHandler<GetInventoryValuationQuery, InventoryValuationDto>
{
    private readonly IStockItemRepository _stock;
    private readonly IWarehouseRepository _warehouses;
    private readonly IProductRepository _products;
    private readonly ICurrentUserService _user;

    public GetInventoryValuationQueryHandler(IStockItemRepository stock, IWarehouseRepository warehouses,
        IProductRepository products, ICurrentUserService user)
    { _stock = stock; _warehouses = warehouses; _products = products; _user = user; }

    public async Task<InventoryValuationDto> Handle(GetInventoryValuationQuery req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;

        // اقلام موجودی: یک انبار یا همهٔ انبارها.
        var items = new List<Domain.Entities.Inventory.StockItem>();
        if (req.WarehouseId is int wid)
            items.AddRange(await _stock.GetByWarehouseAsync(wid, ct));
        else
            foreach (var w in await _warehouses.GetByCompanyAsync(companyId, ct))
                items.AddRange(await _stock.GetByWarehouseAsync(w.Id, ct));

        var names = (await _products.SearchAsync(companyId, string.Empty, ct))
            .ToDictionary(p => p.Id, p => (p.Code, p.Name));

        // تجمیعِ per-product (در حالتِ همهٔ انبارها، موجودیِ یک کالا در چند انبار جمع می‌شود).
        var rows = items
            .GroupBy(s => s.ProductId)
            .Select(g =>
            {
                var qty = g.Sum(x => x.Quantity);
                var value = g.Sum(x => x.Quantity * x.AverageCost);
                var avg = qty != 0 ? value / qty : 0m;
                var (code, name) = names.TryGetValue(g.Key, out var p) ? p : ($"#{g.Key}", "");
                return new StockRow(g.Key, code, name, qty, avg, value);
            })
            .Where(r => string.IsNullOrWhiteSpace(req.Search)
                     || r.Name.Contains(req.Search) || r.Code.Contains(req.Search))
            .OrderBy(r => r.Code)
            .ToList();

        return new InventoryValuationDto(rows, rows.Sum(r => r.Value), rows.Count);
    }
}
