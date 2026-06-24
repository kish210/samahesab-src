using FluentValidation;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Modules.Tourism.Application.Commands;

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

    // نگاشت نقش → کد حساب، مطابقِ نمودار حساب‌های واقعی (database/07_DefaultChartOfAccounts.sql)
    // و سازگار با AccountClassifier: ۱=دارایی، ۳=بدهی، ۶=درآمد، ۷=بهای‌تمام‌شده، ۸=هزینهٔ عملیاتی.
    // (با fallback به حساب‌های والدِ موجود.)
    private static readonly Dictionary<TourismAccountRole, string[]> Codes = new()
    {
        [TourismAccountRole.Cash]              = new[] { "1-01-001", "1-01" },                     // صندوق
        [TourismAccountRole.Receivable]        = new[] { "1-03-001", "1-03", "1-01-001" },         // دریافتنی مشتریان
        [TourismAccountRole.TourismRevenue]    = new[] { "6-01-002", "6-01-001", "6-03", "6-01" }, // درآمد فروش خدمات
        [TourismAccountRole.TourismCost]       = new[] { "7-01-001", "7-01" },                     // بهای تمام‌شدهٔ خدمات
        [TourismAccountRole.SupplierPayable]   = new[] { "3-01-001", "3-01" },                     // پرداختنی تأمین‌کنندگان
        [TourismAccountRole.CommissionExpense] = new[] { "8-11", "8-08", "8-01" },                 // هزینهٔ عملیاتی (کمیسیون)
        [TourismAccountRole.CommissionPayable] = new[] { "3-01-002", "3-01-001", "3-01" },         // پرداختنی سایر
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
