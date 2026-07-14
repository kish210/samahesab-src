using FluentValidation;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Domain.Enums;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Accounting.Commands;

/// <summary>
/// ثبتِ دستیِ چک (BUG-4 — «چک جدید»). چکِ در جریان را ثبت و به طرف‌حساب وصل می‌کند.
/// حسابداری (سازگار با وصولِ موجود):
///   دریافتی (از مشتری): Dr اسنادِ دریافتنی (۱-۰۴-۰۰۱) / Cr حساب‌های دریافتنی (۱-۰۳-۰۰۱) —
///     بدهیِ مشتری به «اسنادِ دریافتنیِ در جریان» منتقل می‌شود؛ وصول بعداً آن را به بانک می‌برد.
///   پرداختی (به تأمین‌کننده): سندی نمی‌خورد — بدهیِ پرداختنی از قبل در دفاتر است و وصولِ چک
///     (ChangeChequeStatus) آن را به بانک می‌برد. (در نمودارِ فعلی، اسناد و حساب‌های پرداختنی یکی‌اند.)
/// </summary>
public record RegisterChequeCommand(
    ChequeType ChequeType, string ChequeNumber, string BankName, decimal Amount, string DueDate,
    int PartyId, string PartyType, string Date,
    string? IssuedBy = null, string? Description = null) : IRequest<Result<int>>;

public class RegisterChequeCommandValidator : AbstractValidator<RegisterChequeCommand>
{
    public RegisterChequeCommandValidator()
    {
        RuleFor(x => x.ChequeNumber).NotEmpty().WithMessage("شمارهٔ چک الزامی است.");
        RuleFor(x => x.BankName).NotEmpty().WithMessage("نامِ بانک الزامی است.");
        RuleFor(x => x.Amount).GreaterThan(0).WithMessage("مبلغِ چک باید بزرگ‌تر از صفر باشد.");
        RuleFor(x => x.DueDate).NotEmpty().WithMessage("تاریخِ سررسید الزامی است.");
        RuleFor(x => x.PartyId).GreaterThan(0).WithMessage("طرف‌حساب الزامی است.");
        RuleFor(x => x.Date).NotEmpty().WithMessage("تاریخِ ثبت الزامی است.");
    }
}

public class RegisterChequeCommandHandler : IRequestHandler<RegisterChequeCommand, Result<int>>
{
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;
    private readonly IChequeRepository _cheques;
    private readonly IAccountRepository _accounts;
    private readonly IVoucherRepository _vouchers;

    public RegisterChequeCommandHandler(IUnitOfWork uow, ICurrentUserService user,
        IChequeRepository cheques, IAccountRepository accounts, IVoucherRepository vouchers)
    { _uow = uow; _user = user; _cheques = cheques; _accounts = accounts; _vouchers = vouchers; }

    public async Task<Result<int>> Handle(RegisterChequeCommand req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        var branchId = _user.BranchId ?? 1;

        await _uow.BeginTransactionAsync(ct);
        try
        {
            var cheque = Cheque.Create(companyId, branchId, req.ChequeType,
                req.ChequeNumber, req.BankName, req.Amount, req.DueDate, req.IssuedBy, req.Description);
            cheque.SetParty(req.PartyId, req.PartyType);

            // فقط چکِ دریافتی سندِ انتقال می‌خورد (پرداختی بعداً هنگامِ وصول سند می‌خورد).
            if (req.ChequeType == ChequeType.Received)
            {
                var notesReceivable = await _accounts.GetByCodeAsync(companyId, "1-04-001", ct);
                var tradeReceivable = await _accounts.GetByCodeAsync(companyId, "1-03-001", ct);
                if (notesReceivable is null || tradeReceivable is null)
                {
                    await _uow.RollbackTransactionAsync(ct);
                    return Result<int>.Failure("حساب‌های «اسنادِ دریافتنی» (۱-۰۴-۰۰۱) یا «حساب‌های دریافتنی» (۱-۰۳-۰۰۱) تعریف نشده‌اند.");
                }

                var number = await _vouchers.GetNextNumberAsync(companyId, ct);
                var v = Voucher.Create(companyId, branchId, 1, number, req.Date,
                    7 /*چک*/, $"ثبتِ چکِ دریافتی شمارهٔ {req.ChequeNumber}");
                v.AddItem(VoucherItem.Create(0, 1, notesReceivable.Id, req.Amount, 0, "اسنادِ دریافتنیِ در جریان"));
                v.AddItem(VoucherItem.Create(0, 2, tradeReceivable.Id, 0, req.Amount, "کاهشِ حسابِ دریافتنیِ مشتری"));
                v.Post(_user.UserId ?? 0);
                await _vouchers.AddAsync(v, ct);
                await _uow.SaveChangesAsync(ct);   // تا Idِ سند ساخته شود

                cheque.SetReceiveVoucher(v.Id);
            }
            else
            {
                // U-ACCT-1.2 — پیش‌تر چکِ پرداختنی هیچ سندی نمی‌زد (بدهیِ آن همچنان زیرِ «پرداختنیِ
                // عمومیِ» ۳-۰۱-۰۰۱ از زمانِ فاکتورِ خرید می‌ماند)؛ یعنی وصولِ چک (ChangeChequeStatus)
                // هم از همان ۳-۰۱-۰۰۱ برمی‌داشت — داخلاً سازگار ولی «اسنادِ پرداختنی» (۳-۰۲-۰۰۱، از
                // قبل seed شده) هرگز استفاده نمی‌شد. حالا همین‌جا بدهی از پرداختنیِ عمومی به اسنادِ
                // پرداختنی-چک بازطبقه‌بندی می‌شود (سندِ استاندارد: صدورِ چک = تبدیلِ بدهیِ باز به یک
                // سندِ قابلِ‌مذاکره)؛ ChangeChequeStatusCommand هم از همین ۳-۰۲-۰۰۱ برمی‌دارد.
                var generalPayable = await _accounts.GetByCodeAsync(companyId, "3-01-001", ct);
                var notesPayable = await _accounts.GetByCodeAsync(companyId, "3-02-001", ct);
                if (generalPayable != null && notesPayable != null)
                {
                    var number = await _vouchers.GetNextNumberAsync(companyId, ct);
                    var v = Voucher.Create(companyId, branchId, 1, number, req.Date,
                        7 /*چک*/, $"صدورِ چکِ پرداختنی شمارهٔ {req.ChequeNumber}");
                    v.AddItem(VoucherItem.Create(0, 1, generalPayable.Id, req.Amount, 0, "کاهشِ حساب‌هایِ پرداختنیِ عمومی"));
                    v.AddItem(VoucherItem.Create(0, 2, notesPayable.Id, 0, req.Amount, "اسنادِ پرداختنی - چک"));
                    v.Post(_user.UserId ?? 0);
                    await _vouchers.AddAsync(v, ct);
                    await _uow.SaveChangesAsync(ct);

                    cheque.SetPayVoucher(v.Id);
                }
                // اگر حساب‌ها تعریف نشده بودند، بی‌سروصدا رد می‌شود (رفتارِ قدیمی: بدونِ سند).
            }

            await _cheques.AddAsync(cheque, ct);
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
}
