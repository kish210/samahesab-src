using MediatR;
using SamaHesab.Application.Automation;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Entities.Inventory;
using SamaHesab.Domain.Entities.Purchase;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Purchase.Commands;

/// <summary>
/// ساخت خودکار «سفارش خرید» از پیشنهادهای نقطه‌ی سفارش (P4→P12).
/// همه‌ی کالاهای زیر آستانه در یک سفارش پیش‌نویس جمع می‌شوند؛ بهای واحد = آخرین بهای خرید کالا.
/// </summary>
public record CreatePurchaseOrderFromReorderCommand(string OrderDate, int? SupplierId = null)
    : IRequest<Result<int>>;

public class CreatePurchaseOrderFromReorderCommandHandler
    : IRequestHandler<CreatePurchaseOrderFromReorderCommand, Result<int>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IRepository<Product> _products;
    private readonly IRepository<StockItem> _stock;
    private readonly IRepository<PurchaseOrder> _orders;

    public CreatePurchaseOrderFromReorderCommandHandler(IUnitOfWork uow, ICurrentUserService currentUser,
        IRepository<Product> products, IRepository<StockItem> stock, IRepository<PurchaseOrder> orders)
    { _uow = uow; _currentUser = currentUser; _products = products; _stock = stock; _orders = orders; }

    public async Task<Result<int>> Handle(CreatePurchaseOrderFromReorderCommand req, CancellationToken ct)
    {
        var companyId = _currentUser.CompanyId ?? 1;
        var branchId = _currentUser.BranchId ?? 1;

        var products = await _products.FindAsync(
            p => p.CompanyId == companyId && (p.MinStock > 0 || p.ReorderPoint > 0), ct);
        var ids = products.Select(p => p.Id).ToList();
        var onHand = (await _stock.FindAsync(s => ids.Contains(s.ProductId), ct))
            .GroupBy(s => s.ProductId).ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

        var suggestions = ReorderEngine.Suggest(products.Select(p => new ReorderInput(
            p.Id, p.Name, onHand.TryGetValue(p.Id, out var q) ? q : 0, p.MinStock, p.ReorderPoint, p.MaxStock)));

        if (suggestions.Count == 0)
            return Result<int>.Failure("هیچ کالایی زیر نقطه‌ی سفارش نیست.");

        var price = products.ToDictionary(p => p.Id, p => p.PurchasePrice);
        var count = await _orders.CountAsync(o => o.CompanyId == companyId, ct);
        var order = PurchaseOrder.Create(companyId, branchId,
            $"PO-{count + 1:0000}", req.OrderDate, req.SupplierId, source: "خودکار",
            description: "سفارش خودکار از نقطه‌ی سفارش");

        foreach (var s in suggestions)
            order.AddItem(s.ProductId, s.SuggestedQty, price.TryGetValue(s.ProductId, out var pr) ? pr : 0);

        await _orders.AddAsync(order, ct);
        await _uow.SaveChangesAsync(ct);
        return Result<int>.Success(order.Id);
    }
}
