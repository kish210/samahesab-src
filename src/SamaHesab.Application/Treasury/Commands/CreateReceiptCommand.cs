using FluentValidation;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Treasury.Commands;

/// <summary>دریافت وجه از مشتری (treasury receipt) — posts a voucher and reduces the customer balance.</summary>
public record CreateReceiptCommand(
    int BranchId, int FiscalYearId, string Date, int CustomerId, decimal Amount,
    string PaymentMethod = "نقدی", string? Description = null) : IRequest<Result<int>>;

public class CreateReceiptCommandValidator : AbstractValidator<CreateReceiptCommand>
{
    public CreateReceiptCommandValidator()
    {
        RuleFor(x => x.Date).NotEmpty().WithMessage("تاریخ الزامی است.");
        RuleFor(x => x.CustomerId).GreaterThan(0).WithMessage("مشتری الزامی است.");
        RuleFor(x => x.Amount).GreaterThan(0).WithMessage("مبلغ باید بزرگتر از صفر باشد.");
    }
}

public class CreateReceiptCommandHandler : IRequestHandler<CreateReceiptCommand, Result<int>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IAccountRepository _accounts;
    private readonly IVoucherRepository _vouchers;
    private readonly IRepository<Customer> _customers;

    public CreateReceiptCommandHandler(IUnitOfWork uow, ICurrentUserService currentUser,
        IAccountRepository accounts, IVoucherRepository vouchers, IRepository<Customer> customers)
    { _uow = uow; _currentUser = currentUser; _accounts = accounts; _vouchers = vouchers; _customers = customers; }

    public async Task<Result<int>> Handle(CreateReceiptCommand req, CancellationToken ct)
    {
        await _uow.BeginTransactionAsync(ct);
        try
        {
            var companyId = _currentUser.CompanyId!.Value;
            var payCode = req.PaymentMethod switch
            {
                "بانک" => "1-02-001",
                "چک"   => "1-04-001",
                _       => "1-01-001" // نقدی → صندوق
            };
            var debitAcc = await _accounts.GetByCodeAsync(companyId, payCode, ct)
                           ?? await _accounts.GetByCodeAsync(companyId, "1-01-001", ct);
            var receivable = await _accounts.GetByCodeAsync(companyId, "1-03-001", ct);
            if (debitAcc == null || receivable == null)
                return Result<int>.Failure("حساب‌های خزانه/دریافتنی تعریف نشده‌اند.");

            var number = await _vouchers.GetNextNumberAsync(companyId, ct);
            var v = Voucher.Create(companyId, req.BranchId, req.FiscalYearId, number, req.Date,
                11 /*دریافت*/, req.Description ?? $"دریافت وجه از مشتری");
            v.AddItem(VoucherItem.Create(0, 1, debitAcc.Id, req.Amount, 0, $"دریافت ({req.PaymentMethod})"));
            v.AddItem(VoucherItem.Create(0, 2, receivable.Id, 0, req.Amount, "بابت بدهی مشتری"));
            await _vouchers.AddAsync(v, ct);

            var customer = await _customers.GetByIdAsync(req.CustomerId, ct);
            if (customer != null) customer.UpdateBalance(customer.Balance - req.Amount);

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
