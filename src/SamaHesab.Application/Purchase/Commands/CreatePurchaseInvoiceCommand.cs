using FluentValidation;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Purchase.Commands;

public record CreatePurchaseInvoiceCommand(
    int BranchId,
    int FiscalYearId,
    string InvoiceDate,
    int SupplierId,
    int WarehouseId,
    string InvoiceType,
    int? OrderId,
    string? DueDate,
    string? Description,
    decimal Shipping,
    decimal OtherCosts,
    List<PurchaseInvoiceItemDto> Items
) : IRequest<Result<int>>;

public record PurchaseInvoiceItemDto(
    int ProductId,
    decimal Quantity,
    decimal UnitPrice,
    decimal DiscountPct,
    decimal TaxPct,
    string? Description,
    int? BatchId,
    string? BatchNumber,
    string? ProductionDate,
    string? ExpiryDate
);

public class CreatePurchaseInvoiceCommandValidator : AbstractValidator<CreatePurchaseInvoiceCommand>
{
    public CreatePurchaseInvoiceCommandValidator()
    {
        RuleFor(x => x.InvoiceDate).NotEmpty().WithMessage("تاریخ فاکتور الزامی است.");
        RuleFor(x => x.SupplierId).GreaterThan(0).WithMessage("تأمین‌کننده الزامی است.");
        RuleFor(x => x.WarehouseId).GreaterThan(0).WithMessage("انبار الزامی است.");
        RuleFor(x => x.Items).NotEmpty().WithMessage("فاکتور باید حداقل یک ردیف داشته باشد.");
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(i => i.ProductId).GreaterThan(0).WithMessage("کالا الزامی است.");
            item.RuleFor(i => i.Quantity).GreaterThan(0).WithMessage("مقدار باید بزرگتر از صفر باشد.");
            item.RuleFor(i => i.UnitPrice).GreaterThanOrEqualTo(0).WithMessage("قیمت واحد نمی‌تواند منفی باشد.");
        });
    }
}

public class CreatePurchaseInvoiceCommandHandler : IRequestHandler<CreatePurchaseInvoiceCommand, Result<int>>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly IStockItemRepository _stockRepository;
    private readonly IProductRepository _productRepository;
    private readonly IAccountRepository _accountRepository;
    private readonly IVoucherRepository _voucherRepository;

    public CreatePurchaseInvoiceCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        IStockItemRepository stockRepository,
        IProductRepository productRepository,
        IAccountRepository accountRepository,
        IVoucherRepository voucherRepository)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _stockRepository = stockRepository;
        _productRepository = productRepository;
        _accountRepository = accountRepository;
        _voucherRepository = voucherRepository;
    }

    public async Task<Result<int>> Handle(CreatePurchaseInvoiceCommand request, CancellationToken ct)
    {
        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            var companyId = _currentUser.CompanyId!.Value;

            // Update stock for each item
            foreach (var item in request.Items)
            {
                var stockItem = await _stockRepository
                    .GetByProductAndWarehouseAsync(item.ProductId, request.WarehouseId, ct);

                if (stockItem == null)
                {
                    stockItem = Domain.Entities.Inventory.StockItem.Create(
                        item.ProductId, request.WarehouseId);
                    await _stockRepository.AddAsync(stockItem, ct);
                }

                stockItem.AddStock(item.Quantity, item.UnitPrice);
                _stockRepository.Update(stockItem);

                // Update product purchase price
                var product = await _productRepository.GetByIdAsync(item.ProductId, ct);
                if (product != null)
                {
                    product.UpdatePrices(item.UnitPrice, product.SalePrice,
                        product.WholesalePrice, product.ConsumerPrice, product.TaxRate);
                    _productRepository.Update(product);
                }
            }

            await _unitOfWork.SaveChangesAsync(ct);

            // ── Automatic accounting voucher (debit inventory, credit payable) ──
            await TryCreatePurchaseVoucherAsync(companyId, request, ct);

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitTransactionAsync(ct);

            return Result<int>.Success(1); // Return invoice ID
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync(ct);
            return Result<int>.Failure(ex.GetBaseException().Message);
        }
    }

    private async Task TryCreatePurchaseVoucherAsync(int companyId,
        CreatePurchaseInvoiceCommand request, CancellationToken ct)
    {
        decimal goods = 0, tax = 0;
        foreach (var i in request.Items)
        {
            var sub = i.Quantity * i.UnitPrice;
            var disc = sub * i.DiscountPct / 100m;
            var afterDisc = sub - disc;
            goods += afterDisc;
            tax += afterDisc * i.TaxPct / 100m;
        }
        var grand = goods + tax + request.Shipping + request.OtherCosts;
        if (grand <= 0) return;

        var inventory = await _accountRepository.GetByCodeAsync(companyId, "1-05-001", ct);
        var payable = await _accountRepository.GetByCodeAsync(companyId, "3-01-001", ct);
        if (inventory == null || payable == null) return; // chart not set up → skip

        var number = await _voucherRepository.GetNextNumberAsync(companyId, ct);
        var voucher = Domain.Entities.Accounting.Voucher.Create(companyId, request.BranchId,
            request.FiscalYearId, number, request.InvoiceDate, 4 /*Purchase*/,
            "سند خودکار فاکتور خرید");
        voucher.AddItem(Domain.Entities.Accounting.VoucherItem.Create(0, 1, inventory.Id, grand, 0, "خرید کالا"));
        voucher.AddItem(Domain.Entities.Accounting.VoucherItem.Create(0, 2, payable.Id, 0, grand, "بدهی به تأمین‌کننده"));
        await _voucherRepository.AddAsync(voucher, ct);
    }
}
