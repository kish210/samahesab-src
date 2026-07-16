using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Inventory.Queries;

// ── لیست انبارها ──────────────────────────────────────────────────────────────
public record WarehouseDto(int Id, string Name);
/// <summary>BranchId — U-BRANCH-BASEDATA: فیلترِ اختیاریِ شعبه، رویِ نتیجه‌ای که از قبل global
/// query filterِ Warehouse (شعبهٔ کاربرِ جاری) اعمال شده — انبارِ بدونِ شعبه = مشترکِ همه.</summary>
public record GetWarehousesQuery(int? BranchId = null) : IRequest<List<WarehouseDto>>;

public class GetWarehousesQueryHandler : IRequestHandler<GetWarehousesQuery, List<WarehouseDto>>
{
    private readonly IWarehouseRepository _warehouses;
    private readonly ICurrentUserService _user;
    public GetWarehousesQueryHandler(IWarehouseRepository w, ICurrentUserService u) { _warehouses = w; _user = u; }

    public async Task<List<WarehouseDto>> Handle(GetWarehousesQuery request, CancellationToken ct)
    {
        var list = (await _warehouses.GetByCompanyAsync(_user.CompanyId ?? 1, ct))
            .Where(w => !request.BranchId.HasValue || w.BranchId == request.BranchId || w.BranchId == null);
        return list.Select(w => new WarehouseDto(w.Id, w.Name)).ToList();
    }
}

// ── موجودی فعلی یک انبار ──────────────────────────────────────────────────────
public record StockRow(int ProductId, string Code, string Name, decimal Quantity, decimal AverageCost, decimal Value);
public record GetWarehouseStockQuery(int WarehouseId, string? Search = null) : IRequest<List<StockRow>>;

public class GetWarehouseStockQueryHandler : IRequestHandler<GetWarehouseStockQuery, List<StockRow>>
{
    private readonly IStockItemRepository _stock;
    private readonly IProductRepository _products;
    private readonly ICurrentUserService _user;
    public GetWarehouseStockQueryHandler(IStockItemRepository s, IProductRepository p, ICurrentUserService u)
    { _stock = s; _products = p; _user = u; }

    public async Task<List<StockRow>> Handle(GetWarehouseStockQuery req, CancellationToken ct)
    {
        var items = await _stock.GetByWarehouseAsync(req.WarehouseId, ct);
        var products = (await _products.SearchAsync(_user.CompanyId ?? 1, string.Empty, ct))
            .ToDictionary(p => p.Id, p => (p.Code, p.Name));
        var rows = items.Select(s =>
        {
            var (code, name) = products.TryGetValue(s.ProductId, out var p) ? p : ($"#{s.ProductId}", "");
            return new StockRow(s.ProductId, code, name, s.Quantity, s.AverageCost, s.Quantity * s.AverageCost);
        });
        if (!string.IsNullOrWhiteSpace(req.Search))
            rows = rows.Where(r => r.Name.Contains(req.Search) || r.Code.Contains(req.Search));
        return rows.OrderBy(r => r.Code).ToList();
    }
}
