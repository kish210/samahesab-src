using FluentValidation;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Enums;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Inventory.Commands;

public record UpdateProductCommand(
    int ProductId,
    string Code,
    string? Barcode,
    string Name,
    string? NameEn,
    int? GroupId,
    int UnitId,
    decimal PurchasePrice,
    decimal SalePrice,
    decimal WholesalePrice,
    decimal ConsumerPrice,
    decimal MinStock,
    decimal? MaxStock,
    bool HasSerial,
    bool HasBatch,
    bool HasExpiry,
    ValuationMethod ValuationMethod,
    decimal TaxRate,
    string? Description,
    byte[]? Image
) : IRequest<Result>;

public class UpdateProductCommandValidator : AbstractValidator<UpdateProductCommand>
{
    public UpdateProductCommandValidator()
    {
        RuleFor(x => x.ProductId).GreaterThan(0);
        RuleFor(x => x.Code).NotEmpty().WithMessage("کد کالا الزامی است.")
            .MaximumLength(30).WithMessage("کد کالا حداکثر ۳۰ کاراکتر.");
        RuleFor(x => x.Name).NotEmpty().WithMessage("نام کالا الزامی است.")
            .MaximumLength(300).WithMessage("نام کالا حداکثر ۳۰۰ کاراکتر.");
        RuleFor(x => x.UnitId).GreaterThan(0).WithMessage("واحد اندازه‌گیری الزامی است.");
        RuleFor(x => x.SalePrice).GreaterThanOrEqualTo(0).WithMessage("قیمت فروش نمی‌تواند منفی باشد.");
        RuleFor(x => x.PurchasePrice).GreaterThanOrEqualTo(0).WithMessage("قیمت خرید نمی‌تواند منفی باشد.");
        RuleFor(x => x.MinStock).GreaterThanOrEqualTo(0).WithMessage("حداقل موجودی نمی‌تواند منفی باشد.");
        RuleFor(x => x.TaxRate).InclusiveBetween(0, 100).WithMessage("نرخ مالیات باید بین ۰ تا ۱۰۰ باشد.");
    }
}

public class UpdateProductCommandHandler : IRequestHandler<UpdateProductCommand, Result>
{
    private readonly IProductRepository _productRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public UpdateProductCommandHandler(
        IProductRepository productRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser)
    {
        _productRepository = productRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
    }

    public async Task<Result> Handle(UpdateProductCommand request, CancellationToken ct)
    {
        try
        {
            var companyId = _currentUser.CompanyId!.Value;

            var product = await _productRepository.GetByIdAsync(request.ProductId, ct);
            if (product is null || product.CompanyId != companyId) return Result.Failure("کالا یافت نشد.");

            var byCode = await _productRepository.GetByCodeAsync(companyId, request.Code, ct);
            if (byCode != null && byCode.Id != product.Id) return Result.Failure("کد کالا تکراری است.");

            if (!string.IsNullOrWhiteSpace(request.Barcode))
            {
                var byBarcode = await _productRepository.GetByBarcodeAsync(companyId, request.Barcode, ct);
                if (byBarcode != null && byBarcode.Id != product.Id) return Result.Failure("بارکد تکراری است.");
            }

            product.UpdateDetails(request.Name, request.NameEn, request.GroupId, null,
                request.Barcode, null, request.Description);
            product.UpdatePrices(request.PurchasePrice, request.SalePrice,
                request.WholesalePrice, request.ConsumerPrice, request.TaxRate);
            product.SetStockLimits(request.MinStock, request.MaxStock, null);
            product.SetTrackingOptions(request.HasSerial, request.HasBatch,
                request.HasExpiry, request.ValuationMethod);
            if (request.Image is { Length: > 0 }) product.SetImage(request.Image);

            _productRepository.Update(product);
            await _unitOfWork.SaveChangesAsync(ct);

            return Result.Success();
        }
        catch (Exception ex)
        {
            return Result.Failure(ex.Message);
        }
    }
}
