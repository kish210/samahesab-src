using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Enums;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Inventory.Queries;

/// <summary>جزئیاتِ کاملِ یک کالا برای فرمِ ویرایش (بارگذاریِ کالای موجود).</summary>
public record ProductDetailDto(int Id, string Code, string? Barcode, string Name, string? NameEn,
    int? GroupId, int UnitId, ProductType ProductType,
    decimal PurchasePrice, decimal SalePrice, decimal WholesalePrice, decimal ConsumerPrice,
    decimal MinStock, decimal? MaxStock, bool HasSerial, bool HasBatch, bool HasExpiry,
    ValuationMethod ValuationMethod, decimal TaxRate, string? Description, byte[]? Image);

public record GetProductByIdQuery(int ProductId) : IRequest<ProductDetailDto?>;

public class GetProductByIdQueryHandler : IRequestHandler<GetProductByIdQuery, ProductDetailDto?>
{
    private readonly IProductRepository _products;
    private readonly ICurrentUserService _currentUser;

    public GetProductByIdQueryHandler(IProductRepository products, ICurrentUserService currentUser)
    { _products = products; _currentUser = currentUser; }

    public async Task<ProductDetailDto?> Handle(GetProductByIdQuery req, CancellationToken ct)
    {
        var companyId = _currentUser.CompanyId ?? 1;
        var p = await _products.GetByIdAsync(req.ProductId, ct);
        if (p is null || p.CompanyId != companyId) return null;

        return new ProductDetailDto(p.Id, p.Code, p.Barcode, p.Name, p.NameEn,
            p.GroupId, p.UnitId, p.ProductType,
            p.PurchasePrice, p.SalePrice, p.WholesalePrice, p.ConsumerPrice,
            p.MinStock, p.MaxStock, p.HasSerial, p.HasBatch, p.HasExpiry,
            p.ValuationMethod, p.TaxRate, p.Description, p.Image);
    }
}
