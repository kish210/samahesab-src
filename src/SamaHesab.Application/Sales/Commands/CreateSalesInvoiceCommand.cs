using FluentValidation;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Entities.Sales;
using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Domain.Enums;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Sales.Commands;

public record CreateSalesInvoiceCommand(
    int BranchId,
    int FiscalYearId,
    string InvoiceDate,
    int CustomerId,
    int WarehouseId,
    InvoiceType InvoiceType,
    string PriceLevel,
    int? SalesRepId,
    string? DueDate,
    string? Description,
    decimal Shipping,
    decimal OtherCosts,
    List<SalesInvoiceItemDto> Items
) : IRequest<Result<int>>;

public record SalesInvoiceItemDto(
    int ProductId,
    decimal Quantity,
    decimal UnitPrice,
    decimal DiscountPct,
    decimal TaxPct,
    string? Description,
    int? BatchId,
    int? SerialId
);

public class CreateSalesInvoiceCommandValidator : AbstractValidator<CreateSalesInvoiceCommand>
{
    public CreateSalesInvoiceCommandValidator()
    {
        RuleFor(x => x.InvoiceDate).NotEmpty().WithMessage("تاریخ فاکتور الزامی است.");
        RuleFor(x => x.CustomerId).GreaterThan(0).WithMessage("مشتری الزامی است.");
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

public class CreateSalesInvoiceCommandHandler : IRequestHandler<CreateSalesInvoiceCommand, Result<int>>
{
    private readonly IRepository<SalesInvoice> _invoiceRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly IPersianCalendarService _calendar;
    private readonly IStockItemRepository _stockRepository;
    private readonly IProductRepository _productRepository;
    private readonly IAccountRepository _accountRepository;
    private readonly IVoucherRepository _voucherRepository;

    public CreateSalesInvoiceCommandHandler(
        IRepository<SalesInvoice> invoiceRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        IPersianCalendarService calendar,
        IStockItemRepository stockRepository,
        IProductRepository productRepository,
        IAccountRepository accountRepository,
        IVoucherRepository voucherRepository)
    {
        _invoiceRepository = invoiceRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _calendar = calendar;
        _stockRepository = stockRepository;
        _productRepository = productRepository;
        _accountRepository = accountRepository;
        _voucherRepository = voucherRepository;
    }

    public async Task<Result<int>> Handle(CreateSalesInvoiceCommand request, CancellationToken ct)
    {
        await _unitOfWork.BeginTransactionAsync(ct);
        try
        {
            var companyId = _currentUser.CompanyId!.Value;

            // Generate invoice number
            var invoiceNumber = await GenerateInvoiceNumberAsync(companyId, request.FiscalYearId, request.InvoiceType, ct);

            var invoice = SalesInvoice.Create(
                companyId, request.BranchId, request.FiscalYearId,
                invoiceNumber, request.InvoiceDate, request.CustomerId, request.WarehouseId,
                request.InvoiceType, request.PriceLevel, request.SalesRepId,
                request.DueDate, request.Description);

            for (int i = 0; i < request.Items.Count; i++)
            {
                var dto = request.Items[i];
                var item = SalesInvoiceItem.Create(
                    0, i + 1, dto.ProductId, dto.Quantity, dto.UnitPrice,
                    dto.DiscountPct, dto.TaxPct, dto.Description, dto.BatchId, dto.SerialId);
                invoice.AddItem(item);
            }

            invoice.SetShipping(request.Shipping, request.OtherCosts);

            await _invoiceRepository.AddAsync(invoice, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            // ── Reduce warehouse stock (only for inventory-tracked products = کالا) ──
            if (request.InvoiceType == Domain.Enums.InvoiceType.Sale)
            {
                foreach (var dto in request.Items)
                {
                    var product = await _productRepository.GetByIdAsync(dto.ProductId, ct);
                    if (product == null || product.ProductType != Domain.Enums.ProductType.Product) continue;

                    var stock = await _stockRepository.GetByProductAndWarehouseAsync(dto.ProductId, request.WarehouseId, ct);
                    if (stock == null || stock.Quantity < dto.Quantity)
                        throw new InvalidOperationException(
                            $"موجودی کافی برای «{product.Name}» در انبار وجود ندارد (موجودی: {stock?.Quantity ?? 0}).");

                    stock.RemoveStock(dto.Quantity);
                    _stockRepository.Update(stock);
                }
            }

            // ── Automatic accounting voucher ──
            await TryCreateSalesVoucherAsync(invoice, companyId, request, ct);

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

    private async Task TryCreateSalesVoucherAsync(SalesInvoice invoice, int companyId,
        CreateSalesInvoiceCommand request, CancellationToken ct)
    {
        if (invoice.InvoiceType != Domain.Enums.InvoiceType.Sale || invoice.GrandTotal <= 0) return;

        var receivable = await _accountRepository.GetByCodeAsync(companyId, "1-03-001", ct);
        var sales = await _accountRepository.GetByCodeAsync(companyId, "6-01-001", ct);
        var vat = await _accountRepository.GetByCodeAsync(companyId, "3-04-001", ct);
        if (receivable == null || sales == null) return; // chart not set up → skip silently

        var number = await _voucherRepository.GetNextNumberAsync(companyId, ct);
        var voucher = Voucher.Create(companyId, request.BranchId, request.FiscalYearId,
            number, request.InvoiceDate, 3 /*Sale*/, $"سند خودکار فاکتور فروش {invoice.InvoiceNumber}", invoice.InvoiceNumber);

        int row = 1;
        voucher.AddItem(VoucherItem.Create(0, row++, receivable.Id, invoice.GrandTotal, 0,
            $"فاکتور فروش {invoice.InvoiceNumber}"));
        var salesAmount = invoice.GrandTotal - invoice.TotalTax;
        voucher.AddItem(VoucherItem.Create(0, row++, sales.Id, 0, salesAmount, "درآمد فروش"));
        if (invoice.TotalTax > 0 && vat != null)
            voucher.AddItem(VoucherItem.Create(0, row++, vat.Id, 0, invoice.TotalTax, "مالیات بر ارزش افزوده"));

        await _voucherRepository.AddAsync(voucher, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        invoice.SetVoucher(voucher.Id);
        _invoiceRepository.Update(invoice);
    }

    private async Task<string> GenerateInvoiceNumberAsync(int companyId, int fiscalYearId,
        InvoiceType type, CancellationToken ct)
    {
        // Simplified - in real implementation call the SP
        var prefix = type switch
        {
            InvoiceType.Sale => "F",
            InvoiceType.SaleReturn => "BR",
            InvoiceType.Quotation => "PF",
            _ => "F"
        };
        var count = await _invoiceRepository.CountAsync(
            i => i.CompanyId == companyId && i.FiscalYearId == fiscalYearId && i.InvoiceType == type, ct);
        return $"{prefix}{(count + 1):D6}";
    }
}
