using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Inventory.Commands;

/// <summary>
/// کارِ ۷ — به‌روزرسانیِ سطوحِ قیمتِ یک کالا از صفحهٔ مدیریتِ لیست‌قیمت
/// (خرید/خردهٔ‌فروش/عمده/مصرف‌کننده). نرخِ مالیات دست‌نخورده می‌ماند.
/// </summary>
public record UpdateProductPricesCommand(
    int ProductId, decimal PurchasePrice, decimal SalePrice,
    decimal WholesalePrice, decimal ConsumerPrice) : IRequest<Result>;

public class UpdateProductPricesCommandHandler : IRequestHandler<UpdateProductPricesCommand, Result>
{
    private readonly IProductRepository _products;
    private readonly IUnitOfWork _uow;

    public UpdateProductPricesCommandHandler(IProductRepository products, IUnitOfWork uow)
    { _products = products; _uow = uow; }

    public async Task<Result> Handle(UpdateProductPricesCommand req, CancellationToken ct)
    {
        if (req.PurchasePrice < 0 || req.SalePrice < 0 || req.WholesalePrice < 0 || req.ConsumerPrice < 0)
            return Result.Failure("قیمت نمی‌تواند منفی باشد.");

        var product = await _products.GetByIdAsync(req.ProductId, ct);
        if (product is null) return Result.Failure("کالا یافت نشد.");

        product.UpdatePrices(req.PurchasePrice, req.SalePrice,
            req.WholesalePrice, req.ConsumerPrice, product.TaxRate);
        _products.Update(product);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
