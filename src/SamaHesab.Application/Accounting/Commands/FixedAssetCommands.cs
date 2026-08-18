using FluentValidation;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Domain.Enums;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Accounting.Commands;

/// <summary>U-FIXED-ASSET — ساختِ داراییِ ثابت.</summary>
public record CreateFixedAssetCommand(
    string Code, string Name, string PurchaseDate, decimal PurchaseCost, decimal SalvageValue,
    int UsefulLifeMonths, DepreciationMethod Method = DepreciationMethod.StraightLine, string? Description = null
) : IRequest<Result<int>>;

/// <summary>U-FIXED-ASSET — ویرایشِ داراییِ ثابت (Idِ مسیر مرجع است).</summary>
public record UpdateFixedAssetCommand(
    int Id, string Name, string PurchaseDate, decimal PurchaseCost, decimal SalvageValue,
    int UsefulLifeMonths, DepreciationMethod Method, string? Description
) : IRequest<Result>;

/// <summary>U-FIXED-ASSET — غیرفعال/فعال‌سازیِ دارایی (حذفِ سخت عمداً نیست؛ تاریخچهٔ استهلاک حفظ می‌شود).</summary>
public record SetFixedAssetActiveCommand(int Id, bool Active) : IRequest<Result>;

/// <summary>
/// U-FIXED-ASSET — اجرایِ استهلاکِ دورهٔ مشخص («yyyy/MM») و صدورِ یک سندِ تجمیعی:
/// بدهکارِ «استهلاک» (8-03) / بستانکارِ «استهلاکِ انباشته» (2-06). مقدارِ بازگشتی Idِ سند است
/// (0 = داراییِ قابلِ استهلاکی در این دوره نبود).
/// </summary>
public record DepreciateFixedAssetsCommand(string PeriodMonth) : IRequest<Result<int>>;

public class CreateFixedAssetCommandValidator : AbstractValidator<CreateFixedAssetCommand>
{
    public CreateFixedAssetCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().WithMessage("کدِ دارایی الزامی است.").MaximumLength(50);
        RuleFor(x => x.Name).NotEmpty().WithMessage("نامِ دارایی الزامی است.").MaximumLength(200);
        RuleFor(x => x.PurchaseDate).NotEmpty().WithMessage("تاریخِ خرید الزامی است.");
        RuleFor(x => x.PurchaseCost).GreaterThanOrEqualTo(0);
        RuleFor(x => x.SalvageValue).GreaterThanOrEqualTo(0);
        RuleFor(x => x.UsefulLifeMonths).GreaterThan(0).WithMessage("عمرِ مفید باید بزرگ‌تر از صفر باشد.");
    }
}

public class CreateFixedAssetCommandHandler : IRequestHandler<CreateFixedAssetCommand, Result<int>>
{
    private readonly IRepository<FixedAsset> _assets;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;

    public CreateFixedAssetCommandHandler(IRepository<FixedAsset> assets, IUnitOfWork uow, ICurrentUserService user)
    { _assets = assets; _uow = uow; _user = user; }

    public async Task<Result<int>> Handle(CreateFixedAssetCommand req, CancellationToken ct)
    {
        try
        {
            var companyId = _user.CompanyId!.Value;
            var existing = await _assets.FindSingleAsync(a => a.CompanyId == companyId && a.Code == req.Code, ct);
            if (existing is not null) return Result<int>.Failure("کدِ دارایی تکراری است.");

            var asset = FixedAsset.Create(companyId, req.Code.Trim(), req.Name.Trim(), req.PurchaseDate,
                req.PurchaseCost, req.SalvageValue, req.UsefulLifeMonths, req.Method, req.Description);
            await _assets.AddAsync(asset, ct);
            await _uow.SaveChangesAsync(ct);
            return Result<int>.Success(asset.Id);
        }
        catch (Exception ex)
        {
            return Result<int>.Failure(ex.GetBaseException().Message);
        }
    }
}

public class UpdateFixedAssetCommandHandler : IRequestHandler<UpdateFixedAssetCommand, Result>
{
    private readonly IRepository<FixedAsset> _assets;
    private readonly IUnitOfWork _uow;

    public UpdateFixedAssetCommandHandler(IRepository<FixedAsset> assets, IUnitOfWork uow)
    { _assets = assets; _uow = uow; }

    public async Task<Result> Handle(UpdateFixedAssetCommand req, CancellationToken ct)
    {
        var asset = await _assets.GetByIdAsync(req.Id, ct);
        if (asset is null) return Result.Failure("دارایی یافت نشد.");

        asset.Update(req.Name.Trim(), req.PurchaseDate, req.PurchaseCost, req.SalvageValue,
            req.UsefulLifeMonths, req.Method, req.Description);
        _assets.Update(asset);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public class SetFixedAssetActiveCommandHandler : IRequestHandler<SetFixedAssetActiveCommand, Result>
{
    private readonly IRepository<FixedAsset> _assets;
    private readonly IUnitOfWork _uow;

    public SetFixedAssetActiveCommandHandler(IRepository<FixedAsset> assets, IUnitOfWork uow)
    { _assets = assets; _uow = uow; }

    public async Task<Result> Handle(SetFixedAssetActiveCommand req, CancellationToken ct)
    {
        var asset = await _assets.GetByIdAsync(req.Id, ct);
        if (asset is null) return Result.Failure("دارایی یافت نشد.");

        if (req.Active) asset.Activate(); else asset.Deactivate();
        _assets.Update(asset);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public class DepreciateFixedAssetsCommandHandler : IRequestHandler<DepreciateFixedAssetsCommand, Result<int>>
{
    private const int AdjustmentVoucherTypeId = 8;   // «تعدیل» در Acc.VoucherTypes
    private const string DepExpenseCode = "8-03";    // استهلاک (هزینه)
    private const string AccumDepCode = "2-06";      // استهلاکِ انباشتهٔ دارایی‌های ثابت

    private readonly IRepository<FixedAsset> _assets;
    private readonly IRepository<FiscalYear> _fiscalYears;
    private readonly IAccountRepository _accounts;
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _user;
    private readonly IUnitOfWork _uow;

    public DepreciateFixedAssetsCommandHandler(
        IRepository<FixedAsset> assets, IRepository<FiscalYear> fiscalYears, IAccountRepository accounts,
        IMediator mediator, ICurrentUserService user, IUnitOfWork uow)
    { _assets = assets; _fiscalYears = fiscalYears; _accounts = accounts; _mediator = mediator; _user = user; _uow = uow; }

    public async Task<Result<int>> Handle(DepreciateFixedAssetsCommand req, CancellationToken ct)
    {
        var companyId = _user.CompanyId!.Value;
        var branchId = _user.BranchId ?? 1;

        // ۱) حساب‌هایِ استهلاک باید در چارت موجود باشند (چارتِ پیش‌فرض دارد).
        var depExpense = await _accounts.GetByCodeAsync(companyId, DepExpenseCode, ct);
        var accumDep = await _accounts.GetByCodeAsync(companyId, AccumDepCode, ct);
        if (depExpense is null || accumDep is null)
            return Result<int>.Failure($"حسابِ استهلاک ({DepExpenseCode}/{AccumDepCode}) در چارتِ حساب یافت نشد.");

        // ۲) سالِ مالیِ فعال (سندِ استهلاک باید در دورهٔ درست ثبت شود).
        var fiscalYearId = await FiscalYearResolver.ResolveActiveIdAsync(_fiscalYears, companyId, ct);

        var targetMonth = DepreciationCalculator.TotalMonths(req.PeriodMonth);
        var assets = await _assets.FindAsync(a => a.CompanyId == companyId && a.IsActive, ct);

        decimal total = 0;
        var affected = new List<FixedAsset>();
        foreach (var asset in assets)
        {
            var startMonth = asset.LastDepreciatedMonth is { } lm
                ? DepreciationCalculator.TotalMonths(lm + "/01")
                : DepreciationCalculator.TotalMonths(asset.PurchaseDate);
            var monthsToRun = targetMonth - startMonth;
            if (monthsToRun <= 0) continue;

            var amount = DepreciationCalculator.DepreciationForMonths(
                asset.PurchaseCost, asset.SalvageValue, asset.UsefulLifeMonths,
                asset.Method, asset.AccumulatedDepreciation, monthsToRun);
            if (amount <= 0) continue;

            asset.ApplyDepreciation(amount, req.PeriodMonth);
            affected.Add(asset);
            total += amount;
        }

        if (total <= 0) return Result<int>.Success(0);

        // ۳) سندِ تجمیعی: بدهکارِ 8-03 / بستانکارِ 2-06.
        var voucherDate = req.PeriodMonth + "/01";
        var items = new List<VoucherItemDto>
        {
            new(1, depExpense.Id, total, 0, $"استهلاکِ ماهانهٔ دارایی‌های ثابت — {req.PeriodMonth}", null, null),
            new(2, accumDep.Id, 0, total, $"استهلاکِ انباشته — {req.PeriodMonth}", null, null),
        };

        var created = await _mediator.Send(new CreateVoucherCommand(
            branchId, fiscalYearId, voucherDate, AdjustmentVoucherTypeId,
            $"استهلاکِ ماهانهٔ دارایی‌های ثابت ({req.PeriodMonth})", req.PeriodMonth,
            null, 1, items), ct);
        if (!created.Succeeded)
            return Result<int>.Failure($"صدورِ سندِ استهلاک ناموفق بود: {created.ErrorMessage}");

        await _mediator.Send(new PostVoucherCommand(created.Value), ct);
        await _uow.SaveChangesAsync(ct);

        return Result<int>.Success(created.Value);
    }
}
