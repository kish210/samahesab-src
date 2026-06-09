using FluentValidation;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Domain.Enums;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Accounting.Commands;

/// <summary>
/// تغییر وضعیت چکِ در جریان: وصول / برگشت / واگذاری.
/// وصول، سند حسابداری خودکار می‌زند (انتقال از «چک در جریان» به «بانک»).
/// </summary>
public record ChangeChequeStatusCommand(
    int ChequeId,
    ChequeAction Action,
    string Date,
    string? ReturnReason = null) : IRequest<Result<int>>;

public enum ChequeAction { Clear, Return, Transfer }

public class ChangeChequeStatusCommandValidator : AbstractValidator<ChangeChequeStatusCommand>
{
    public ChangeChequeStatusCommandValidator()
    {
        RuleFor(x => x.ChequeId).GreaterThan(0);
        RuleFor(x => x.Date).NotEmpty().WithMessage("تاریخ الزامی است.");
        RuleFor(x => x.ReturnReason)
            .NotEmpty().When(x => x.Action == ChequeAction.Return)
            .WithMessage("علت برگشت چک الزامی است.");
    }
}

public class ChangeChequeStatusCommandHandler : IRequestHandler<ChangeChequeStatusCommand, Result<int>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _currentUser;
    private readonly IChequeRepository _cheques;
    private readonly IAccountRepository _accounts;
    private readonly IVoucherRepository _vouchers;

    public ChangeChequeStatusCommandHandler(IUnitOfWork uow, ICurrentUserService currentUser,
        IChequeRepository cheques, IAccountRepository accounts, IVoucherRepository vouchers)
    { _uow = uow; _currentUser = currentUser; _cheques = cheques; _accounts = accounts; _vouchers = vouchers; }

    public async Task<Result<int>> Handle(ChangeChequeStatusCommand req, CancellationToken ct)
    {
        await _uow.BeginTransactionAsync(ct);
        try
        {
            var companyId = _currentUser.CompanyId!.Value;
            var cheque = await _cheques.GetByIdAsync(req.ChequeId, ct);
            if (cheque == null) return Result<int>.Failure("چک یافت نشد.");

            switch (req.Action)
            {
                case ChequeAction.Return:
                    cheque.Return(req.ReturnReason!);
                    break;

                case ChequeAction.Transfer:
                    cheque.Transfer();
                    break;

                case ChequeAction.Clear:
                    var voucherId = await CreateClearingVoucherAsync(companyId, cheque, req.Date, ct);
                    if (voucherId == 0)
                        return Result<int>.Failure("حساب‌های بانک/چک تعریف نشده‌اند.");
                    cheque.Clear(voucherId);
                    break;
            }

            await _uow.SaveChangesAsync(ct);
            await _uow.CommitTransactionAsync(ct);
            return Result<int>.Success(cheque.Id);
        }
        catch (Exception ex)
        {
            await _uow.RollbackTransactionAsync(ct);
            return Result<int>.Failure(ex.GetBaseException().Message);
        }
    }

    /// <summary>
    /// سند وصول چک:
    /// دریافتی → بدهکار بانک / بستانکار چک‌های دریافتنی در جریان.
    /// پرداختی → بدهکار اسناد پرداختنی / بستانکار بانک.
    /// </summary>
    private async Task<int> CreateClearingVoucherAsync(int companyId, Cheque cheque, string date, CancellationToken ct)
    {
        var bank = await _accounts.GetByCodeAsync(companyId, "1-02-001", ct);
        var chequeReceivable = await _accounts.GetByCodeAsync(companyId, "1-04-001", ct);
        var payable = await _accounts.GetByCodeAsync(companyId, "3-01-001", ct);
        if (bank == null || chequeReceivable == null || payable == null) return 0;

        var number = await _vouchers.GetNextNumberAsync(companyId, ct);
        var v = Voucher.Create(companyId, cheque.BranchId, 1, number, date,
            7 /*چک*/, $"وصول چک شماره {cheque.ChequeNumber}");

        if (cheque.ChequeType == ChequeType.Received)
        {
            v.AddItem(VoucherItem.Create(0, 1, bank.Id, cheque.Amount, 0, "واریز چک دریافتی"));
            v.AddItem(VoucherItem.Create(0, 2, chequeReceivable.Id, 0, cheque.Amount, "خروج از چک‌های در جریان"));
        }
        else // Paid
        {
            v.AddItem(VoucherItem.Create(0, 1, payable.Id, cheque.Amount, 0, "تسویه چک پرداختی"));
            v.AddItem(VoucherItem.Create(0, 2, bank.Id, 0, cheque.Amount, "برداشت بابت چک پرداختی"));
        }

        await _vouchers.AddAsync(v, ct);
        await _uow.SaveChangesAsync(ct);   // تا شناسه‌ی واقعی سند برای پیوند به چک تولید شود
        return v.Id;
    }
}
