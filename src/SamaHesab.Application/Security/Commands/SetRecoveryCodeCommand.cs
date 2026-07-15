using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Application.Common.Security;
using SamaHesab.Domain.Entities.Security;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Security.Commands;

/// <summary>
/// U-SEC-RECOVERY — ذخیرهٔ کدِ بازیابیِ رمز که در ویزاردِ راه‌اندازیِ اولیه ساخته و به کاربر نشان
/// داده شده (خودِ کد اینجا فقط برایِ هش‌شدن می‌آید، هرگز ذخیره/لاگ نمی‌شود).
/// </summary>
public record SetRecoveryCodeCommand(int UserId, string RecoveryCode) : IRequest<Result>;

public class SetRecoveryCodeCommandHandler : IRequestHandler<SetRecoveryCodeCommand, Result>
{
    private readonly IRepository<User> _users;
    private readonly IUnitOfWork _uow;
    public SetRecoveryCodeCommandHandler(IRepository<User> users, IUnitOfWork uow)
    { _users = users; _uow = uow; }

    public async Task<Result> Handle(SetRecoveryCodeCommand req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.RecoveryCode) || req.RecoveryCode.Length < 8)
            return Result.Failure("کدِ بازیابی نامعتبر است.");

        var user = await _users.GetByIdAsync(req.UserId, ct);
        if (user is null) return Result.Failure("کاربر یافت نشد.");

        var (hash, salt) = PasswordHasher.Create(req.RecoveryCode);
        user.SetRecoveryCode(hash, salt);
        _users.Update(user);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
