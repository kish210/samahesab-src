using FluentValidation;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Domain.Enums;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Accounting.Commands;

/// <summary>
/// ایجاد/ویرایشِ حساب در نمودار حساب‌ها (هستهٔ ERP).
/// Id=null → ایجاد (سطح از روی والد تعیین می‌شود، کد یکتا)؛ Id دارد → ویرایشِ نام/شرح/ماهیت.
/// </summary>
public record SaveAccountCommand(
    int? Id, string Code, string Name, string Nature, string AccountType,
    int? ParentId, string? Description = null) : IRequest<Result<int>>;

public class SaveAccountCommandValidator : AbstractValidator<SaveAccountCommand>
{
    public SaveAccountCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().WithMessage("کد حساب الزامی است.");
        RuleFor(x => x.Name).NotEmpty().WithMessage("نام حساب الزامی است.");
    }
}

public class SaveAccountCommandHandler : IRequestHandler<SaveAccountCommand, Result<int>>
{
    private readonly IAccountRepository _accounts;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;

    public SaveAccountCommandHandler(IAccountRepository accounts, IUnitOfWork uow, ICurrentUserService user)
    { _accounts = accounts; _uow = uow; _user = user; }

    public async Task<Result<int>> Handle(SaveAccountCommand req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        var nature = (req.Nature == "بستانکار" || req.Nature == "Credit") ? AccountNature.Credit : AccountNature.Debit;

        // ── ویرایش ──
        if (req.Id is int id && id > 0)
        {
            var acc = await _accounts.GetByIdAsync(id, ct);
            if (acc == null) return Result<int>.Failure("حساب یافت نشد.");
            acc.Update(req.Name, req.Description, nature);
            _accounts.Update(acc);
            await _uow.SaveChangesAsync(ct);
            return Result<int>.Success(acc.Id);
        }

        // ── ایجاد ──
        var existing = await _accounts.GetByCodeAsync(companyId, req.Code, ct);
        if (existing != null) return Result<int>.Failure($"حسابی با کد «{req.Code}» از قبل وجود دارد.");

        // سطح از روی والد (والد+۱)؛ بدون والد = گروه. حداکثر تا تفصیلی.
        var level = AccountLevel.Group;
        if (req.ParentId is int pid)
        {
            var parent = await _accounts.GetByIdAsync(pid, ct);
            if (parent != null)
                level = (AccountLevel)System.Math.Min((int)parent.Level + 1, (int)AccountLevel.Detail);
        }

        var account = Account.Create(companyId, req.Code, req.Name, level, nature,
            req.AccountType, req.ParentId, req.Description);
        await _accounts.AddAsync(account, ct);
        await _uow.SaveChangesAsync(ct);
        return Result<int>.Success(account.Id);
    }
}
