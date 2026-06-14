using FluentValidation;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Treasury.Commands;

/// <summary>
/// سندِ تسویهٔ بین‌شعبه (inter-branch transfer) — انتقالِ وجه از یک شعبه به شعبهٔ دیگر.
/// دو سندِ قطعیِ متوازن می‌سازد (یکی per شعبه) با «حساب جاریِ فی‌مابینِ شعب» به‌عنوان واسط؛
/// خالصِ حساب فی‌مابین روی کلِ شرکت صفر می‌شود.
///   • شعبهٔ مبدأ:  بدهکار «فی‌مابین» / بستانکار «صندوق‌یا‌بانکِ مبدأ»  (وجه از مبدأ خارج می‌شود)
///   • شعبهٔ مقصد:  بدهکار «صندوق‌یا‌بانکِ مقصد» / بستانکار «فی‌مابین» (وجه به مقصد وارد می‌شود)
/// </summary>
public record CreateInterBranchTransferCommand(
    int FromBranchId,
    int ToBranchId,
    int FiscalYearId,
    string Date,
    decimal Amount,
    int FromAccountId,
    int ToAccountId,
    int? InterBranchAccountId = null,
    string? Description = null
) : IRequest<Result<InterBranchTransferResult>>;

public record InterBranchTransferResult(int FromVoucherId, int ToVoucherId, string Reference);

public class CreateInterBranchTransferCommandValidator : AbstractValidator<CreateInterBranchTransferCommand>
{
    public CreateInterBranchTransferCommandValidator()
    {
        RuleFor(x => x.Date).NotEmpty().WithMessage("تاریخ الزامی است.");
        RuleFor(x => x.Amount).GreaterThan(0).WithMessage("مبلغ باید بزرگتر از صفر باشد.");
        RuleFor(x => x.FromBranchId).GreaterThan(0).WithMessage("شعبهٔ مبدأ الزامی است.");
        RuleFor(x => x.ToBranchId).GreaterThan(0).WithMessage("شعبهٔ مقصد الزامی است.");
        RuleFor(x => x).Must(x => x.FromBranchId != x.ToBranchId)
            .WithMessage("شعبهٔ مبدأ و مقصد نمی‌توانند یکسان باشند.");
        RuleFor(x => x.FromAccountId).GreaterThan(0).WithMessage("حسابِ مبدأ (صندوق/بانک) الزامی است.");
        RuleFor(x => x.ToAccountId).GreaterThan(0).WithMessage("حسابِ مقصد (صندوق/بانک) الزامی است.");
    }
}

public class CreateInterBranchTransferCommandHandler
    : IRequestHandler<CreateInterBranchTransferCommand, Result<InterBranchTransferResult>>
{
    // کدِ قراردادیِ حساب جاریِ فی‌مابینِ شعب (seed در مهاجرتِ 25_InterBranchAccount.sql).
    private const string InterBranchCode = "1-07-001";
    // نوعِ سندِ «عمومی» (Acc.VoucherTypes: GEN = نهمین رکوردِ seed).
    private const int GeneralVoucherTypeId = 9;

    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IAccountRepository _accounts;
    private readonly IVoucherRepository _vouchers;
    private readonly IRepository<FiscalYear> _fiscalYears;

    public CreateInterBranchTransferCommandHandler(
        IUnitOfWork uow, ICurrentUserService currentUser, IAccountRepository accounts,
        IVoucherRepository vouchers, IRepository<FiscalYear> fiscalYears)
    {
        _uow = uow; _currentUser = currentUser; _accounts = accounts;
        _vouchers = vouchers; _fiscalYears = fiscalYears;
    }

    public async Task<Result<InterBranchTransferResult>> Handle(
        CreateInterBranchTransferCommand req, CancellationToken ct)
    {
        await _uow.BeginTransactionAsync(ct);
        try
        {
            var companyId = _currentUser.CompanyId!.Value;
            var userId = _currentUser.UserId ?? 0;

            // قفل دوره (مانند CreateVoucherCommand): سال مالیِ بسته/خارج‌از‌بازه مسدود است.
            var fy = await _fiscalYears.GetByIdAsync(req.FiscalYearId, ct);
            if (fy is not null)
            {
                if (fy.IsClosed)
                    return Fail($"سال مالی «{fy.Title}» بسته شده است؛ ثبت سند مجاز نیست.");
                if (!fy.Contains(req.Date))
                    return Fail($"تاریخ ({req.Date}) خارج از بازهٔ سال مالی «{fy.Title}» است.");
            }

            // حسابِ واسطِ فی‌مابین: یا صریحاً پاس داده می‌شود یا با کدِ قراردادی پیدا می‌شود.
            var interBranch = req.InterBranchAccountId is int ibId
                ? await _accounts.GetByIdAsync(ibId, ct)
                : await _accounts.GetByCodeAsync(companyId, InterBranchCode, ct);
            if (interBranch == null)
                return Fail("حساب جاریِ فی‌مابینِ شعب تعریف نشده است (کد 1-07-001). لطفاً مهاجرتِ پایگاه‌داده را اجرا کنید.");

            var from = await _accounts.GetByIdAsync(req.FromAccountId, ct);
            var to = await _accounts.GetByIdAsync(req.ToAccountId, ct);
            if (from == null || to == null)
                return Fail("حسابِ مبدأ یا مقصد یافت نشد.");

            var desc = string.IsNullOrWhiteSpace(req.Description)
                ? "انتقال وجه بین‌شعبه" : req.Description!;

            // سندِ شعبهٔ مبدأ: بد فی‌مابین / بس صندوق‌بانکِ مبدأ.
            var numFrom = await _vouchers.GetNextNumberAsync(companyId, ct);
            var reference = $"IBT-{numFrom}";
            var vFrom = Voucher.Create(companyId, req.FromBranchId, req.FiscalYearId, numFrom,
                req.Date, GeneralVoucherTypeId, $"{desc} (ارسال به شعبهٔ مقصد)", reference);
            vFrom.AddItem(VoucherItem.Create(0, 1, interBranch.Id, req.Amount, 0, "بدهیِ شعبهٔ مقصد (فی‌مابین)"));
            vFrom.AddItem(VoucherItem.Create(0, 2, from.Id, 0, req.Amount, $"خروجِ وجه از {from.Name}"));
            vFrom.Post(userId);
            await _vouchers.AddAsync(vFrom, ct);

            // سندِ شعبهٔ مقصد: بد صندوق‌بانکِ مقصد / بس فی‌مابین.
            var numTo = await _vouchers.GetNextNumberAsync(companyId, ct);
            var vTo = Voucher.Create(companyId, req.ToBranchId, req.FiscalYearId, numTo,
                req.Date, GeneralVoucherTypeId, $"{desc} (دریافت از شعبهٔ مبدأ)", reference);
            vTo.AddItem(VoucherItem.Create(0, 1, to.Id, req.Amount, 0, $"ورودِ وجه به {to.Name}"));
            vTo.AddItem(VoucherItem.Create(0, 2, interBranch.Id, 0, req.Amount, "طلبِ از شعبهٔ مبدأ (فی‌مابین)"));
            vTo.Post(userId);
            await _vouchers.AddAsync(vTo, ct);

            await _uow.SaveChangesAsync(ct);
            await _uow.CommitTransactionAsync(ct);
            return Result<InterBranchTransferResult>.Success(
                new InterBranchTransferResult(vFrom.Id, vTo.Id, reference));
        }
        catch (Exception ex)
        {
            await _uow.RollbackTransactionAsync(ct);
            return Fail(ex.GetBaseException().Message);
        }
    }

    private static Result<InterBranchTransferResult> Fail(string msg)
        => Result<InterBranchTransferResult>.Failure(msg);
}
