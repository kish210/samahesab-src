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
    List<PurchaseInvoiceItemDto> Items,
    decimal PaidAmount = 0
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
    private readonly IRepository<Domain.Entities.Purchase.PurchaseInvoice> _invoiceRepository;
    private readonly IRepository<Domain.Entities.Inventory.StockTransaction> _ledger;

    public CreatePurchaseInvoiceCommandHandler(
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        IStockItemRepository stockRepository,
        IProductRepository productRepository,
        IAccountRepository accountRepository,
        IVoucherRepository voucherRepository,
        IRepository<Domain.Entities.Purchase.PurchaseInvoice> invoiceRepository,
        IRepository<Domain.Entities.Inventory.StockTransaction> ledger)
    {
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _stockRepository = stockRepository;
        _productRepository = productRepository;
        _accountRepository = accountRepository;
        _voucherRepository = voucherRepository;
        _invoiceRepository = invoiceRepository;
        _ledger = ledger;
    }

    public async Task<Result<int>> Handle(CreatePurchaseInvoiceCommand request, CancellationToken ct)
    {
        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            var companyId = _currentUser.CompanyId!.Value;

            // ── Persist the purchase invoice record ──
            var invoiceNumber = await GenerateInvoiceNumberAsync(companyId, request.FiscalYearId, ct);
            var invoice = Domain.Entities.Purchase.PurchaseInvoice.Create(
                companyId, request.BranchId, request.FiscalYearId, invoiceNumber,
                request.InvoiceDate, request.SupplierId, request.WarehouseId,
                request.InvoiceType, request.OrderId, request.DueDate, request.Description);

            for (int i = 0; i < request.Items.Count; i++)
            {
                var d = request.Items[i];
                invoice.AddItem(Domain.Entities.Purchase.PurchaseInvoiceItem.Create(
                    0, i + 1, d.ProductId, d.Quantity, d.UnitPrice, d.DiscountPct, d.TaxPct,
                    d.Description, d.BatchId, null));
            }
            invoice.SetShipping(request.Shipping, request.OtherCosts);
            await _invoiceRepository.AddAsync(invoice, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            // Update stock for each item
            foreach (var item in request.Items)
            {
                var stockItem = await _stockRepository
                    .GetByProductAndWarehouseAsync(item.ProductId, request.WarehouseId, ct);

                var isNew = stockItem == null;
                if (isNew)
                {
                    stockItem = Domain.Entities.Inventory.StockItem.Create(
                        item.ProductId, request.WarehouseId);
                }

                stockItem!.AddStock(item.Quantity, item.UnitPrice);

                if (isNew)
                    await _stockRepository.AddAsync(stockItem, ct);
                else
                    _stockRepository.Update(stockItem);

                // kardex ledger entry (inflow)
                await _ledger.AddAsync(Domain.Entities.Inventory.StockTransaction.Create(
                    companyId, request.BranchId, "ورود خرید", invoiceNumber, request.InvoiceDate,
                    item.ProductId, request.WarehouseId, item.Quantity, item.UnitPrice,
                    stockItem.Quantity, stockItem.Quantity * stockItem.AverageCost,
                    "PurchaseInvoice", invoice.Id, null), ct);

                // Update product purchase price
                var product = await _productRepository.GetByIdAsync(item.ProductId, ct);
                if (product != null)
                {
                    product.UpdatePrices(item.UnitPrice, product.SalePrice,
                        product.WholesalePrice, product.ConsumerPrice, product.TaxRate);
                    _productRepository.Update(product);
                }
            }

            invoice.SetPaid(request.PaidAmount);
            await _unitOfWork.SaveChangesAsync(ct);

            // ── Automatic accounting voucher (debit inventory, credit payable) ──
            var voucherId = await TryCreatePurchaseVoucherAsync(companyId, request, ct);
            if (voucherId.HasValue) invoice.Post(voucherId.Value);
            _invoiceRepository.Update(invoice);

            await _unitOfWork.SaveChangesAsync(ct);
            await _unitOfWork.CommitTransactionAsync(ct);

            return Result<int>.Success(invoice.Id);
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync(ct);
            return Result<int>.Failure(ex.GetBaseException().Message);
        }
    }

    private async Task<int?> TryCreatePurchaseVoucherAsync(int companyId,
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
        if (grand <= 0) return null;

        var inventory = await _accountRepository.GetByCodeAsync(companyId, "1-05-001", ct);
        var payable = await _accountRepository.GetByCodeAsync(companyId, "3-01-001", ct);
        if (inventory == null || payable == null) return null; // chart not set up → skip

        var number = await _voucherRepository.GetNextNumberAsync(companyId, ct);
        var voucher = Domain.Entities.Accounting.Voucher.Create(companyId, request.BranchId,
            request.FiscalYearId, number, request.InvoiceDate, 4 /*Purchase*/,
            "سند خودکار فاکتور خرید");
        voucher.AddItem(Domain.Entities.Accounting.VoucherItem.Create(0, 1, inventory.Id, grand, 0, "خرید کالا"));

        // split credit between cash paid now and the remaining payable to supplier
        var paid = request.PaidAmount > 0 ? System.Math.Min(request.PaidAmount, grand) : 0;
        var remain = grand - paid;
        int row = 2;
        if (paid > 0)
        {
            var cash = await _accountRepository.GetByCodeAsync(companyId, "1-01-001", ct);
            if (cash != null)
                voucher.AddItem(Domain.Entities.Accounting.VoucherItem.Create(0, row++, cash.Id, 0, paid, "پرداخت نقدی به تأمین‌کننده"));
            else remain = grand;
        }
        if (remain > 0)
            voucher.AddItem(Domain.Entities.Accounting.VoucherItem.Create(0, row++, payable.Id, 0, remain, "بدهی به تأمین‌کننده"));

        await _voucherRepository.AddAsync(voucher, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return voucher.Id;
    }

    private async Task<string> GenerateInvoiceNumberAsync(int companyId, int fiscalYearId, CancellationToken ct)
    {
        var count = await _invoiceRepository.CountAsync(
            i => i.CompanyId == companyId && i.FiscalYearId == fiscalYearId, ct);
        return $"PF{(count + 1):D6}";
    }
}
