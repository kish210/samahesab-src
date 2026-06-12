using FluentValidation;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Tourism.Commands;

/// <summary>
/// تولید خودکار سند حسابداری از یک عملیات گردشگری (M11) — بدون ورود دستیِ سند.
/// ماژول گردشگری (وقتی رزرو قطعی می‌شود) این Command را با داده‌ی مالی صدا می‌زند.
/// </summary>
public record GenerateTourismVoucherCommand(
    int BranchId, int FiscalYearId, string Date, string BookingRef,
    decimal SaleAmount, decimal CostAmount, decimal CommissionPercent, decimal PaidAmount,
    string PaymentMethod = "نقدی") : IRequest<Result<int>>;

public class GenerateTourismVoucherCommandValidator : AbstractValidator<GenerateTourismVoucherCommand>
{
    public GenerateTourismVoucherCommandValidator()
    {
        RuleFor(x => x.Date).NotEmpty().WithMessage("تاریخ الزامی است.");
        RuleFor(x => x.SaleAmount).GreaterThan(0).WithMessage("مبلغ فروش باید بزرگتر از صفر باشد.");
    }
}

public class GenerateTourismVoucherCommandHandler
    : IRequestHandler<GenerateTourismVoucherCommand, Result<int>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IAccountRepository _accounts;
    private readonly IVoucherRepository _vouchers;

    public GenerateTourismVoucherCommandHandler(IUnitOfWork uow, ICurrentUserService currentUser,
        IAccountRepository accounts, IVoucherRepository vouchers)
    { _uow = uow; _currentUser = currentUser; _accounts = accounts; _vouchers = vouchers; }

    // نگاشت نقش → کد حساب (با fallback به حساب‌های پایه‌ی موجود)
    private static readonly Dictionary<TourismAccountRole, string[]> Codes = new()
    {
        [TourismAccountRole.Cash]              = new[] { "1-01-001" },
        [TourismAccountRole.Receivable]        = new[] { "1-03-001", "1-01-001" },
        [TourismAccountRole.TourismRevenue]    = new[] { "4-02", "4-01" },
        [TourismAccountRole.TourismCost]       = new[] { "5-03", "5-01" },
        [TourismAccountRole.SupplierPayable]   = new[] { "3-01-001" },
        [TourismAccountRole.CommissionExpense] = new[] { "5-04", "5-02", "5-01" },
        [TourismAccountRole.CommissionPayable] = new[] { "3-02-001", "3-01-001" },
    };

    public async Task<Result<int>> Handle(GenerateTourismVoucherCommand req, CancellationToken ct)
    {
        var companyId = _currentUser.CompanyId!.Value;
        var settlement = TourismSettlement.Build(req.SaleAmount, req.CostAmount, req.CommissionPercent, req.PaidAmount);
        if (!settlement.IsBalanced)
            return Result<int>.Failure("سند گردشگری متوازن نشد.");

        await _uow.BeginTransactionAsync(ct);
        try
        {
            // resolve همه‌ی حساب‌های موردنیاز
            var accId = new Dictionary<TourismAccountRole, int>();
            foreach (var role in settlement.Lines.Select(l => l.Role).Distinct())
            {
                Account? acc = null;
                foreach (var code in Codes[role])
                {
                    acc = await _accounts.GetByCodeAsync(companyId, code, ct);
                    if (acc != null) break;
                }
                if (acc == null)
                    return Result<int>.Failure($"حساب موردنیاز ({role}) در نمودار حساب‌ها تعریف نشده است.");
                accId[role] = acc.Id;
            }

            var number = await _vouchers.GetNextNumberAsync(companyId, ct);
            var v = Voucher.Create(companyId, req.BranchId, req.FiscalYearId, number, req.Date,
                9 /*عمومی*/, $"سند خودکار گردشگری — رزرو {req.BookingRef}");

            var row = 1;
            foreach (var line in settlement.Lines)
                v.AddItem(VoucherItem.Create(0, row++, accId[line.Role], line.Debit, line.Credit, line.Description));

            await _vouchers.AddAsync(v, ct);
            await _uow.SaveChangesAsync(ct);
            await _uow.CommitTransactionAsync(ct);
            return Result<int>.Success(v.Id);
        }
        catch (Exception ex)
        {
            await _uow.RollbackTransactionAsync(ct);
            return Result<int>.Failure(ex.GetBaseException().Message);
        }
    }
}
