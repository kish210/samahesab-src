using FluentValidation;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Domain.Enums;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Accounting.Commands;

/// <summary>
/// U-LOAN — ثبتِ تسهیلاتِ مالی/وام. هم‌زمان یک سندِ «دریافت» می‌زند:
/// بدهکارِ «صندوق» / بستانکارِ «تسهیلات» (کوتاه‌مدت ≤ ۱۲ ماه، بلندمدت &gt; ۱۲ ماه).
/// </summary>
public record CreateLoanCommand(
    string Code, string Name, string StartDate,
    decimal Principal, decimal AnnualInterestPercent, int TermMonths
) : IRequest<Result<int>>;

/// <summary>
/// U-LOAN — پرداختِ قسطِ بعدی (ترتیبی) و صدورِ سندِ «پرداخت»:
/// بدهکارِ «تسهیلات» (اصل) + «هزینه‌های مالی» (بهره) / بستانکارِ «صندوق». مقدارِ بازگشتی Idِ سند است.
/// </summary>
public record PayLoanInstallmentCommand(int Id, int InstallmentIndex, string PaymentDate) : IRequest<Result<int>>;

public class CreateLoanCommandValidator : AbstractValidator<CreateLoanCommand>
{
    public CreateLoanCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().WithMessage("کدِ وام الزامی است.").MaximumLength(50);
        RuleFor(x => x.Name).NotEmpty().WithMessage("نامِ وام الزامی است.").MaximumLength(200);
        RuleFor(x => x.StartDate).NotEmpty().WithMessage("تاریخِ دریافت الزامی است.");
        RuleFor(x => x.Principal).GreaterThan(0).WithMessage("اصلِ وام باید بزرگ‌تر از صفر باشد.");
        RuleFor(x => x.AnnualInterestPercent).GreaterThanOrEqualTo(0);
        RuleFor(x => x.TermMonths).GreaterThan(0).WithMessage("مدتِ وام باید بزرگ‌تر از صفر باشد.");
    }
}

public class CreateLoanCommandHandler : IRequestHandler<CreateLoanCommand, Result<int>>
{
    private const int ReceiptVoucherTypeId = 11;   // RCV — دریافت
    private const string CashCode = "1-01-001";     // صندوق
    private const string ShortTermLoanCode = "3-07"; // تسهیلات کوتاه‌مدت
    private const string LongTermLoanCode = "4-01";  // تسهیلات بلندمدت

    private readonly IRepository<Loan> _loans;
    private readonly IRepository<FiscalYear> _fiscalYears;
    private readonly IAccountRepository _accounts;
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _user;
    private readonly IUnitOfWork _uow;

    public CreateLoanCommandHandler(
        IRepository<Loan> loans, IRepository<FiscalYear> fiscalYears, IAccountRepository accounts,
        IMediator mediator, ICurrentUserService user, IUnitOfWork uow)
    { _loans = loans; _fiscalYears = fiscalYears; _accounts = accounts; _mediator = mediator; _user = user; _uow = uow; }

    public async Task<Result<int>> Handle(CreateLoanCommand req, CancellationToken ct)
    {
        try
        {
            var companyId = _user.CompanyId!.Value;
            var branchId = _user.BranchId ?? 1;

            var existing = await _loans.FindSingleAsync(l => l.CompanyId == companyId && l.Code == req.Code, ct);
            if (existing is not null) return Result<int>.Failure("کدِ وام تکراری است.");

            var loan = Loan.Create(companyId, req.Code.Trim(), req.Name.Trim(), req.StartDate,
                req.Principal, req.AnnualInterestPercent, req.TermMonths);
            await _loans.AddAsync(loan, ct);

            // سندِ دریافتِ وام: بد «صندوق» / بس «تسهیلات».
            var cash = await _accounts.GetByCodeAsync(companyId, CashCode, ct);
            var loanPayableCode = req.TermMonths <= 12 ? ShortTermLoanCode : LongTermLoanCode;
            var loanPayable = await _accounts.GetByCodeAsync(companyId, loanPayableCode, ct);
            if (cash is null || loanPayable is null)
                return Result<int>.Failure($"حسابِ صندوق ({CashCode}) یا تسهیلات ({loanPayableCode}) در چارت یافت نشد.");

            var fiscalYearId = await FiscalYearResolver.ResolveActiveIdAsync(_fiscalYears, companyId, ct);
            var items = new List<VoucherItemDto>
            {
                new(1, cash.Id, req.Principal, 0, $"دریافتِ وام — {req.Name}", null, null),
                new(2, loanPayable.Id, 0, req.Principal, $"تسهیلات — {req.Name}", null, null),
            };

            var created = await _mediator.Send(new CreateVoucherCommand(
                branchId, fiscalYearId, req.StartDate, ReceiptVoucherTypeId,
                $"دریافتِ وام ({req.Code})", req.Code, null, 1, items), ct);
            if (!created.Succeeded)
                return Result<int>.Failure($"صدورِ سندِ دریافتِ وام ناموفق بود: {created.ErrorMessage}");

            await _mediator.Send(new PostVoucherCommand(created.Value), ct);
            await _uow.SaveChangesAsync(ct);

            return Result<int>.Success(loan.Id);
        }
        catch (Exception ex)
        {
            return Result<int>.Failure(ex.GetBaseException().Message);
        }
    }
}

public class PayLoanInstallmentCommandHandler : IRequestHandler<PayLoanInstallmentCommand, Result<int>>
{
    private const int PaymentVoucherTypeId = 10;   // PAY — پرداخت
    private const string CashCode = "1-01-001";     // صندوق
    private const string ShortTermLoanCode = "3-07"; // تسهیلات کوتاه‌مدت
    private const string LongTermLoanCode = "4-01";  // تسهیلات بلندمدت
    private const string InterestExpenseCode = "8-10"; // هزینه‌های مالی

    private readonly IRepository<Loan> _loans;
    private readonly IRepository<FiscalYear> _fiscalYears;
    private readonly IAccountRepository _accounts;
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _user;
    private readonly IUnitOfWork _uow;

    public PayLoanInstallmentCommandHandler(
        IRepository<Loan> loans, IRepository<FiscalYear> fiscalYears, IAccountRepository accounts,
        IMediator mediator, ICurrentUserService user, IUnitOfWork uow)
    { _loans = loans; _fiscalYears = fiscalYears; _accounts = accounts; _mediator = mediator; _user = user; _uow = uow; }

    public async Task<Result<int>> Handle(PayLoanInstallmentCommand req, CancellationToken ct)
    {
        var companyId = _user.CompanyId!.Value;
        var branchId = _user.BranchId ?? 1;

        var loan = await _loans.GetByIdAsync(req.Id, ct);
        if (loan is null) return Result<int>.Failure("وام یافت نشد.");
        if (loan.Status == LoanStatus.Closed) return Result<int>.Failure("وام تسویه شده است.");
        if (req.InstallmentIndex != loan.PaidInstallments + 1)
            return Result<int>.Failure($"قسط‌ها باید به‌ترتیب پرداخت شوند — قسطِ بعدی {loan.PaidInstallments + 1} است.");
        if (req.InstallmentIndex > loan.TermMonths) return Result<int>.Failure("شمارهٔ قسط نامعتبر است.");

        var schedule = LoanCalculator.BuildSchedule(loan.Principal, loan.AnnualInterestPercent, loan.TermMonths);
        var inst = schedule.First(i => i.Index == req.InstallmentIndex);

        // حساب‌ها.
        var cash = await _accounts.GetByCodeAsync(companyId, CashCode, ct);
        var loanPayableCode = loan.TermMonths <= 12 ? ShortTermLoanCode : LongTermLoanCode;
        var loanPayable = await _accounts.GetByCodeAsync(companyId, loanPayableCode, ct);
        if (cash is null || loanPayable is null)
            return Result<int>.Failure($"حسابِ صندوق ({CashCode}) یا تسهیلات ({loanPayableCode}) در چارت یافت نشد.");

        Account? interestExpense = null;
        if (inst.Interest > 0)
        {
            interestExpense = await _accounts.GetByCodeAsync(companyId, InterestExpenseCode, ct);
            if (interestExpense is null)
                return Result<int>.Failure($"حسابِ هزینه‌های مالی ({InterestExpenseCode}) در چارت یافت نشد.");
        }

        // سندِ پرداختِ قسط: بد تسهیلات (اصل) + هزینهٔ بهره / بس صندوق.
        var items = new List<VoucherItemDto>
        {
            new(1, loanPayable.Id, inst.Principal, 0, $"اصلِ قسطِ {req.InstallmentIndex} — {loan.Name}", null, null),
        };
        var row = 2;
        if (interestExpense is not null)
            items.Add(new VoucherItemDto(row++, interestExpense.Id, inst.Interest, 0,
                $"بهرهٔ قسطِ {req.InstallmentIndex} — {loan.Name}", null, null));
        items.Add(new VoucherItemDto(row, cash.Id, 0, inst.Payment, $"پرداختِ قسطِ {req.InstallmentIndex} — {loan.Name}", null, null));

        var fiscalYearId = await FiscalYearResolver.ResolveActiveIdAsync(_fiscalYears, companyId, ct);
        var created = await _mediator.Send(new CreateVoucherCommand(
            branchId, fiscalYearId, req.PaymentDate, PaymentVoucherTypeId,
            $"پرداختِ قسطِ {req.InstallmentIndex}/{loan.TermMonths} وام ({loan.Code})",
            loan.Code, null, 1, items), ct);
        if (!created.Succeeded)
            return Result<int>.Failure($"صدورِ سندِ پرداختِ قسط ناموفق بود: {created.ErrorMessage}");

        await _mediator.Send(new PostVoucherCommand(created.Value), ct);

        loan.RecordPayment(req.InstallmentIndex, inst.Principal, inst.Interest, req.PaymentDate);
        _loans.Update(loan);
        await _uow.SaveChangesAsync(ct);

        return Result<int>.Success(created.Value);
    }
}
