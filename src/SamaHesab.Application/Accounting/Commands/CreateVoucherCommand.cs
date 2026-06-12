using FluentValidation;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Accounting.Commands;

public record CreateVoucherCommand(
    int BranchId,
    int FiscalYearId,
    string VoucherDate,
    int VoucherTypeId,
    string? Description,
    string? Reference,
    int? CurrencyId,
    decimal ExchangeRate,
    List<VoucherItemDto> Items
) : IRequest<Result<int>>;

public record VoucherItemDto(
    int RowNumber,
    int AccountId,
    decimal Debit,
    decimal Credit,
    string? Description,
    int? CostCenterId,
    int? ProjectId
);

public class CreateVoucherCommandValidator : AbstractValidator<CreateVoucherCommand>
{
    public CreateVoucherCommandValidator()
    {
        RuleFor(x => x.VoucherDate).NotEmpty().WithMessage("تاریخ سند الزامی است.");
        RuleFor(x => x.VoucherTypeId).GreaterThan(0).WithMessage("نوع سند الزامی است.");
        RuleFor(x => x.Items).NotEmpty().WithMessage("سند باید حداقل یک ردیف داشته باشد.");
        RuleFor(x => x.Items).Must(items =>
            Math.Abs(items.Sum(i => i.Debit) - items.Sum(i => i.Credit)) < 0.01m)
            .WithMessage("سند تراز نیست. مجموع بدهکار و بستانکار باید برابر باشد.");
    }
}

public class CreateVoucherCommandHandler : IRequestHandler<CreateVoucherCommand, Result<int>>
{
    private readonly IVoucherRepository _voucherRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;
    private readonly IRepository<FiscalYear> _fiscalYears;

    public CreateVoucherCommandHandler(
        IVoucherRepository voucherRepository,
        IUnitOfWork unitOfWork,
        ICurrentUserService currentUser,
        IRepository<FiscalYear> fiscalYears)
    {
        _voucherRepository = voucherRepository;
        _unitOfWork = unitOfWork;
        _currentUser = currentUser;
        _fiscalYears = fiscalYears;
    }

    public async Task<Result<int>> Handle(CreateVoucherCommand request, CancellationToken ct)
    {
        try
        {
            var companyId = _currentUser.CompanyId!.Value;

            // قفل دوره: اگر سال مالی تعریف شده باشد، بسته‌بودن/خارج‌از‌بازه‌بودن مسدود می‌شود.
            // (نبودِ رکورد سال مالی برای نصب‌های قدیمی مجاز است تا چیزی نشکند.)
            var fy = await _fiscalYears.GetByIdAsync(request.FiscalYearId, ct);
            if (fy is not null)
            {
                if (fy.IsClosed)
                    return Result<int>.Failure($"سال مالی «{fy.Title}» بسته شده است؛ ثبت سند مجاز نیست.");
                if (!fy.Contains(request.VoucherDate))
                    return Result<int>.Failure(
                        $"تاریخ سند ({request.VoucherDate}) خارج از بازهٔ سال مالی «{fy.Title}» ({fy.StartDate} تا {fy.EndDate}) است.");
            }

            var voucherNumber = await _voucherRepository.GetNextNumberAsync(companyId, ct);

            var voucher = Voucher.Create(
                companyId, request.BranchId, request.FiscalYearId,
                voucherNumber, request.VoucherDate, request.VoucherTypeId,
                request.Description, request.Reference);

            foreach (var itemDto in request.Items.OrderBy(i => i.RowNumber))
            {
                var item = VoucherItem.Create(
                    0, itemDto.RowNumber, itemDto.AccountId,
                    itemDto.Debit, itemDto.Credit, itemDto.Description,
                    request.CurrencyId, request.ExchangeRate,
                    itemDto.CostCenterId, itemDto.ProjectId);
                voucher.AddItem(item);
            }

            await _voucherRepository.AddAsync(voucher, ct);
            await _unitOfWork.SaveChangesAsync(ct);

            return Result<int>.Success(voucher.Id);
        }
        catch (Exception ex)
        {
            return Result<int>.Failure(ex.GetBaseException().Message);
        }
    }
}
