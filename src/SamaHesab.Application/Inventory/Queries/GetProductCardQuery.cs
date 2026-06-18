using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Inventory.Queries;

/// <summary>کارت کالا (۳۶۰°): مشخصات/قیمت/کنترل + موجودیِ چنداِنباره — منبعِ واحد (API + دسکتاپ).</summary>
public record ProductCardStockRow(string WarehouseName, decimal Quantity, bool IsLow);

public record ProductCardDto(
    int Id, string Code, string Name, string? Barcode, bool IsActive,
    decimal PurchasePrice, decimal SalePrice, decimal WholesalePrice, decimal ConsumerPrice, decimal TaxRate,
    decimal MinStock, decimal? MaxStock, decimal? ReorderPoint, string Tracking,
    decimal TotalStock, List<ProductCardStockRow> WarehouseStocks);

public record GetProductCardQuery(int ProductId) : IRequest<ProductCardDto?>;

public class GetProductCardQueryHandler : IRequestHandler<GetProductCardQuery, ProductCardDto?>
{
    private readonly IProductRepository _products;
    private readonly IWarehouseRepository _warehouses;
    private readonly IStockItemRepository _stock;
    private readonly ICurrentUserService _currentUser;

    public GetProductCardQueryHandler(IProductRepository products, IWarehouseRepository warehouses,
        IStockItemRepository stock, ICurrentUserService currentUser)
    { _products = products; _warehouses = warehouses; _stock = stock; _currentUser = currentUser; }

    public async Task<ProductCardDto?> Handle(GetProductCardQuery req, CancellationToken ct)
    {
        var p = await _products.GetByIdAsync(req.ProductId, ct);
        if (p == null) return null;

        var companyId = _currentUser.CompanyId ?? 1;
        var whName = (await _warehouses.GetByCompanyAsync(companyId, ct)).ToDictionary(w => w.Id, w => w.Name);
        var stock = await _stock.GetByProductAsync(req.ProductId, ct);

        var rows = stock.GroupBy(s => s.WarehouseId).Select(g =>
        {
            var qty = g.Sum(x => x.Quantity);
            return new ProductCardStockRow(whName.TryGetValue(g.Key, out var n) ? n : $"#{g.Key}", qty, qty < p.MinStock);
        }).ToList();

        var tracking = p.HasSerial ? "سریال" : p.HasBatch ? "بچ" : "ندارد";
        return new ProductCardDto(p.Id, p.Code, p.Name, p.Barcode, p.IsActive,
            p.PurchasePrice, p.SalePrice, p.WholesalePrice, p.ConsumerPrice, p.TaxRate,
            p.MinStock, p.MaxStock, p.ReorderPoint, tracking,
            stock.Sum(s => s.Quantity), rows);
    }
}
