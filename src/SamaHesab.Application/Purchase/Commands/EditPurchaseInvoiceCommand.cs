using FluentValidation;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Entities.Purchase;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Purchase.Commands;

/// <summary>قرینهٔ `EditSalesInvoiceCommand` برایِ فاکتورِ خرید — همان الگویِ «مرجوعیِ کامل + صدورِ
/// فاکتورِ نو» با Commandهایِ ازقبل‌تست‌شدهٔ `CreatePurchaseReturnCommand`/`CreatePurchaseInvoiceCommand`.</summary>
public record EditPurchaseInvoiceCommand(
    int InvoiceId, string InvoiceDate, int SupplierId, int WarehouseId,
    string? Description, decimal Shipping, decimal OtherCosts,
    List<PurchaseInvoiceItemDto> Items, decimal PaidAmount = 0
) : IRequest<Result<int>>;

public class EditPurchaseInvoiceCommandValidator : AbstractValidator<EditPurchaseInvoiceCommand>
{
    public EditPurchaseInvoiceCommandValidator()
    {
        RuleFor(x => x.InvoiceId).GreaterThan(0);
        RuleFor(x => x.InvoiceDate).NotEmpty().WithMessage("تاریخ الزامی است.");
        RuleFor(x => x.SupplierId).GreaterThan(0).WithMessage("تأمین‌کننده الزامی است.");
        RuleFor(x => x.WarehouseId).GreaterThan(0).WithMessage("انبار الزامی است.");
        RuleFor(x => x.Items).NotEmpty().WithMessage("فاکتور باید حداقل یک ردیف داشته باشد.");
    }
}

public class EditPurchaseInvoiceCommandHandler : IRequestHandler<EditPurchaseInvoiceCommand, Result<int>>
{
    private readonly IRepository<PurchaseInvoice> _invoices;
    private readonly IRepository<PurchaseInvoiceItem> _items;
    private readonly IMediator _mediator;

    public EditPurchaseInvoiceCommandHandler(IRepository<PurchaseInvoice> invoices, IRepository<PurchaseInvoiceItem> items, IMediator mediator)
    { _invoices = invoices; _items = items; _mediator = mediator; }

    public async Task<Result<int>> Handle(EditPurchaseInvoiceCommand req, CancellationToken ct)
    {
        var original = await _invoices.GetByIdAsync(req.InvoiceId, ct);
        if (original is null) return Result<int>.Failure("فاکتور یافت نشد.");
        if (original.InvoiceType == "برگشت خرید")
            return Result<int>.Failure("فقط فاکتورِ خریدِ عادی قابلِ ویرایش است (نه مرجوعی).");
        // ReturnedFromId رویِ خودِ سندِ مرجوعی نشسته (نه رویِ اصلی) — قرینهٔ رفعِ همینِ باگ در EditSalesInvoiceCommand.
        var alreadyReturned = await _invoices.FindSingleAsync(i => i.ReturnedFromId == original.Id, ct);
        if (alreadyReturned != null)
            return Result<int>.Failure("این فاکتور قبلاً ویرایش یا مرجوع شده است.");
        if (original.PaidAmount != 0)
            return Result<int>.Failure("این فاکتور پرداختی/دریافتیِ ثبت‌شده دارد — ابتدا از «دریافت/پرداخت» یا مرجوعی، آن را برگردانید، سپس ویرایش کنید.");

        var originalLines = (await _items.FindAsync(l => l.InvoiceId == req.InvoiceId, ct)).ToList();
        if (originalLines.Count == 0) return Result<int>.Failure("فاکتورِ اصلی هیچ ردیفی ندارد.");

        var returnItems = originalLines.Select(l => new PurchaseReturnItemDto(l.ProductId, l.Quantity, l.UnitPrice, l.TaxPct)).ToList();
        var returnResult = await _mediator.Send(new CreatePurchaseReturnCommand(
            original.BranchId, original.FiscalYearId, req.InvoiceDate, original.SupplierId, original.WarehouseId,
            returnItems, $"مرجوعیِ خودکار برایِ ویرایشِ فاکتورِ {original.InvoiceNumber}",
            RefundCash: false, OriginalInvoiceId: original.Id), ct);
        if (!returnResult.Succeeded) return Result<int>.Failure($"مرجوعیِ فاکتورِ اصلی ناموفق بود: {returnResult.ErrorMessage}");

        var createResult = await _mediator.Send(new CreatePurchaseInvoiceCommand(
            original.BranchId, original.FiscalYearId, req.InvoiceDate, req.SupplierId, req.WarehouseId,
            original.InvoiceType, original.OrderId, original.DueDate,
            string.IsNullOrWhiteSpace(req.Description) ? $"ویرایشِ فاکتورِ {original.InvoiceNumber}" : req.Description,
            req.Shipping, req.OtherCosts, req.Items, req.PaidAmount), ct);
        if (!createResult.Succeeded)
            return Result<int>.Failure(
                $"مرجوعیِ فاکتورِ اصلی انجام شد ولی صدورِ فاکتورِ نو ناموفق بود: {createResult.ErrorMessage} " +
                "— لطفاً یک فاکتورِ خریدِ معمولی با دادهٔ درست ثبت کنید (مرجوعی برگشت‌ناپذیر نیست، از «فاکتورهایِ خرید» قابلِ پیگیری است).");

        return Result<int>.Success(createResult.Value);
    }
}
