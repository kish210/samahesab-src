using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Inventory.Commands;

/// <summary>
/// U-WEB-DEACTIVATE — قرینهٔ `DeactivateProductCommand` (که از قبل بود، در GetProductsQuery.cs)؛
/// آن Command فقط یک‌طرفه بود (غیرفعال‌سازی) و هیچ راهِ برگشتی نداشت.
/// </summary>
public record ActivateProductCommand(int ProductId) : IRequest<Result>;

public class ActivateProductCommandHandler : IRequestHandler<ActivateProductCommand, Result>
{
    private readonly IProductRepository _products;
    private readonly IUnitOfWork _uow;
    public ActivateProductCommandHandler(IProductRepository products, IUnitOfWork uow)
    { _products = products; _uow = uow; }

    public async Task<Result> Handle(ActivateProductCommand req, CancellationToken ct)
    {
        var p = await _products.GetByIdAsync(req.ProductId, ct);
        if (p == null) return Result.Failure("کالا یافت نشد.");
        p.Activate();
        _products.Update(p);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
