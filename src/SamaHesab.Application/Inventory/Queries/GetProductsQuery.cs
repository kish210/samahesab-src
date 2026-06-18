using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Inventory.Queries;

/// <summary>
/// کالاها — فهرستِ کاملِ لیست (با هشدارِ کسری). منبعِ واحدِ منطق:
/// هم API (ProductsController) و هم دسکتاپ از همین کوئری می‌خوانند (الگوی API-only).
/// </summary>
public record ProductRowDto(int Id, string Code, string Barcode, string Name,
    decimal SalePrice, decimal PurchasePrice, decimal WholesalePrice,
    decimal MinStock, bool IsActive, bool IsLowStock,
    decimal ConsumerPrice = 0, decimal TaxRate = 0);

public record GetProductsQuery(string? Search = null) : IRequest<List<ProductRowDto>>;

public class GetProductsQueryHandler : IRequestHandler<GetProductsQuery, List<ProductRowDto>>
{
    private readonly IProductRepository _products;
    private readonly ICurrentUserService _currentUser;

    public GetProductsQueryHandler(IProductRepository products, ICurrentUserService currentUser)
    { _products = products; _currentUser = currentUser; }

    public async Task<List<ProductRowDto>> Handle(GetProductsQuery req, CancellationToken ct)
    {
        var companyId = _currentUser.CompanyId ?? 1;
        var list = await _products.SearchAsync(companyId, req.Search ?? string.Empty, ct);
        var lowStock = (await _products.GetLowStockAsync(companyId, ct)).Select(p => p.Id).ToHashSet();

        return list.Select(p => new ProductRowDto(
            p.Id, p.Code, p.Barcode ?? "", p.Name,
            p.SalePrice, p.PurchasePrice, p.WholesalePrice,
            p.MinStock, p.IsActive, lowStock.Contains(p.Id),
            p.ConsumerPrice, p.TaxRate)).ToList();
    }
}

/// <summary>غیرفعال‌سازیِ کالا (حذفِ نرم) — از طریقِ کامند، نه دسترسیِ مستقیمِ ViewModel به repo.</summary>
public record DeactivateProductCommand(int ProductId) : IRequest<Result>;

public class DeactivateProductCommandHandler : IRequestHandler<DeactivateProductCommand, Result>
{
    private readonly IProductRepository _products;
    private readonly IUnitOfWork _uow;

    public DeactivateProductCommandHandler(IProductRepository products, IUnitOfWork uow)
    { _products = products; _uow = uow; }

    public async Task<Result> Handle(DeactivateProductCommand req, CancellationToken ct)
    {
        var p = await _products.GetByIdAsync(req.ProductId, ct);
        if (p == null) return Result.Failure("کالا یافت نشد.");
        p.Deactivate();
        _products.Update(p);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
