using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Application.Common.Security;
using SamaHesab.Domain.Entities.Security;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Security.Commands;

/// <summary>
/// فاز ۱۲ G3 — تغییرِ رمزِ یک کاربر (برای اجبارِ تغییرِ رمزِ پیش‌فرضِ admin در ویزاردِ راه‌اندازی).
/// رمزِ جدید با همان PasswordHasher هش و روی موجودیتِ User ست می‌شود (MustChangePass=false).
/// </summary>
public record ChangeUserPasswordCommand(int UserId, string NewPassword) : IRequest<Result>;

public class ChangeUserPasswordCommandHandler : IRequestHandler<ChangeUserPasswordCommand, Result>
{
    private readonly IRepository<User> _users;
    private readonly IUnitOfWork _uow;
    public ChangeUserPasswordCommandHandler(IRepository<User> users, IUnitOfWork uow)
    { _users = users; _uow = uow; }

    public async Task<Result> Handle(ChangeUserPasswordCommand req, CancellationToken ct)
    {
        // RC-2 — سیاستِ رمزِ تجاری (حداقل ۸ + حرف و رقم).
        var pw = SamaHesab.Application.Common.Validation.PasswordPolicy.Validate(req.NewPassword);
        if (!pw.Ok) return Result.Failure(pw.Error!);

        var user = await _users.GetByIdAsync(req.UserId, ct);
        if (user is null) return Result.Failure("کاربر یافت نشد.");

        var (hash, salt) = PasswordHasher.Create(req.NewPassword);
        user.SetPassword(hash, salt);
        _users.Update(user);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
