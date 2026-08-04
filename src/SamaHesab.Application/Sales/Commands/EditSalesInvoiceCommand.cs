using FluentValidation;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Entities.Sales;
using SamaHesab.Domain.Enums;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Sales.Commands;

/// <summary>
/// U-WEB-INV-EDIT — «ویرایشِ فاکتور». چون فاکتورِ وب بلافاصله Confirm+Post می‌شود (بدونِ حالتِ
/// Draftِ باقی‌مانده) و `SalesInvoice.Cancel()` صراحتاً فاکتورِ Posted را رد می‌کند (تصمیمِ
/// آگاهانهٔ مدلِ دامنه، نه محدودیتِ فنی)، «ویرایش» به‌جایِ تغییرِ درجای سندِ حسابداری‌شده، از
/// الگویِ استانداردِ حسابداری «مرجوعیِ کامل + صدورِ سندِ نو» استفاده می‌کند — همان دو Commandِ
/// ازقبل‌تست‌شدهٔ `CreateSalesReturnCommand`/`CreateSalesInvoiceCommand` را ارکستر می‌کند، بدونِ
/// نوشتنِ منطقِ نوی GL/انبار. فاکتورِ اصلی به‌عنوانِ رکوردِ تاریخیِ دست‌نخورده می‌ماند (استانداردِ
/// حسابرسی)؛ اثرِ مالی‌اش با مرجوعیِ کامل خنثی و فاکتورِ نو با دادهٔ ویرایش‌شده صادر می‌شود.
/// </summary>
public record EditSalesInvoiceCommand(
    int InvoiceId, string InvoiceDate, int CustomerId, int WarehouseId, string PriceLevel,
    string? Description, decimal Shipping, decimal OtherCosts, decimal InvoiceDiscount,
    List<SalesInvoiceItemDto> Items, string PaymentMethod = "نسیه", decimal PaidAmount = 0
) : IRequest<Result<int>>;

public class EditSalesInvoiceCommandValidator : AbstractValidator<EditSalesInvoiceCommand>
{
    public EditSalesInvoiceCommandValidator()
    {
        RuleFor(x => x.InvoiceId).GreaterThan(0);
        RuleFor(x => x.InvoiceDate).NotEmpty().WithMessage("تاریخ الزامی است.");
        RuleFor(x => x.CustomerId).GreaterThan(0).WithMessage("مشتری الزامی است.");
        RuleFor(x => x.WarehouseId).GreaterThan(0).WithMessage("انبار الزامی است.");
        RuleFor(x => x.Items).NotEmpty().WithMessage("فاکتور باید حداقل یک ردیف داشته باشد.");
    }
}

public class EditSalesInvoiceCommandHandler : IRequestHandler<EditSalesInvoiceCommand, Result<int>>
{
    private readonly IRepository<SalesInvoice> _invoices;
    private readonly IRepository<SalesInvoiceItem> _items;
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _user;

    public EditSalesInvoiceCommandHandler(IRepository<SalesInvoice> invoices, IRepository<SalesInvoiceItem> items,
        IMediator mediator, ICurrentUserService user)
    { _invoices = invoices; _items = items; _mediator = mediator; _user = user; }

    public async Task<Result<int>> Handle(EditSalesInvoiceCommand req, CancellationToken ct)
    {
        var original = await _invoices.GetByIdAsync(req.InvoiceId, ct);
        if (original is null) return Result<int>.Failure("فاکتور یافت نشد.");
        if (original.InvoiceType != InvoiceType.Sale)
            return Result<int>.Failure("فقط فاکتورِ فروشِ عادی قابلِ ویرایش است (نه مرجوعی/پیش‌فاکتور).");
        if (original.Status == InvoiceStatus.Cancelled)
            return Result<int>.Failure("این فاکتور لغو شده است.");
        // ReturnedFromId رویِ خودِ سندِ مرجوعی نشسته (نه رویِ اصلی) — برایِ فهمیدنِ «قبلاً
        // ویرایش/مرجوع شده یا نه» باید دنبالِ مرجوعی‌ای گشت که ReturnedFromId اش به همین فاکتور اشاره کند.
        var alreadyReturned = await _invoices.FindSingleAsync(i => i.ReturnedFromId == original.Id, ct);
        if (alreadyReturned != null)
            return Result<int>.Failure("این فاکتور قبلاً ویرایش یا مرجوع شده است.");
        if (original.PaidAmount != 0)
            return Result<int>.Failure("این فاکتور پرداختی/دریافتیِ ثبت‌شده دارد — ابتدا از «دریافت/پرداخت» یا مرجوعی، آن را برگردانید، سپس ویرایش کنید.");

        var originalLines = (await _items.FindAsync(l => l.InvoiceId == req.InvoiceId, ct)).ToList();
        if (originalLines.Count == 0) return Result<int>.Failure("فاکتورِ اصلی هیچ ردیفی ندارد.");

        // گامِ ۱ — مرجوعیِ کاملِ فاکتورِ اصلی (خنثی‌سازیِ اثرِ مالی/انباری؛ خودِ سندِ اصلی دست‌نخورده می‌ماند).
        var returnItems = originalLines.Select(l => new SalesReturnItemDto(l.ProductId, l.Quantity, l.UnitPrice, l.TaxPct)).ToList();
        var returnResult = await _mediator.Send(new CreateSalesReturnCommand(
            original.BranchId, original.FiscalYearId, req.InvoiceDate, original.CustomerId, original.WarehouseId,
            returnItems, $"مرجوعیِ خودکار برایِ ویرایشِ فاکتورِ {original.InvoiceNumber}",
            RefundCash: false, OriginalInvoiceId: original.Id), ct);
        if (!returnResult.Succeeded) return Result<int>.Failure($"مرجوعیِ فاکتورِ اصلی ناموفق بود: {returnResult.ErrorMessage}");

        // گامِ ۲ — صدورِ فاکتورِ نو با دادهٔ ویرایش‌شده.
        var createResult = await _mediator.Send(new CreateSalesInvoiceCommand(
            original.BranchId, original.FiscalYearId, req.InvoiceDate, req.CustomerId, req.WarehouseId,
            InvoiceType.Sale, req.PriceLevel, original.SalesRepId, original.DueDate,
            string.IsNullOrWhiteSpace(req.Description) ? $"ویرایشِ فاکتورِ {original.InvoiceNumber}" : req.Description,
            req.Shipping, req.OtherCosts, req.Items, req.InvoiceDiscount, req.PaidAmount, req.PaymentMethod), ct);
        if (!createResult.Succeeded)
            return Result<int>.Failure(
                $"مرجوعیِ فاکتورِ اصلی انجام شد ولی صدورِ فاکتورِ نو ناموفق بود: {createResult.ErrorMessage} " +
                "— لطفاً یک فاکتورِ فروشِ معمولی با دادهٔ درست ثبت کنید (مرجوعی برگشت‌ناپذیر نیست، از «فاکتورهایِ فروش» قابلِ پیگیری است).");

        return Result<int>.Success(createResult.Value);
    }
}
