using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Application.Common.Security;
using SamaHesab.Domain.Entities.Security;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Security.Commands;

/// <summary>
/// U-SEC-RECOVERY — بازنشانیِ رمزِ فراموش‌شده با کدِ بازیابی (بدونِ نیازِ ایمیل/پیامک؛ این برنامه
/// آفلاین/محلی است). پیامِ خطا عمداً مبهم است («نامِ کاربری یا کدِ بازیابی نادرست») تا وجود/عدمِ
/// یک نامِ کاربریِ خاص را برایِ مهاجم فاش نکند.
/// </summary>
public record ResetPasswordWithRecoveryCodeCommand(
    int CompanyId, string Username, string RecoveryCode, string NewPassword) : IRequest<Result>;

public class ResetPasswordWithRecoveryCodeCommandHandler : IRequestHandler<ResetPasswordWithRecoveryCodeCommand, Result>
{
    private const string GenericError = "نامِ کاربری یا کدِ بازیابی نادرست است.";

    private readonly IRepository<User> _users;
    private readonly IUnitOfWork _uow;
    public ResetPasswordWithRecoveryCodeCommandHandler(IRepository<User> users, IUnitOfWork uow)
    { _users = users; _uow = uow; }

    public async Task<Result> Handle(ResetPasswordWithRecoveryCodeCommand req, CancellationToken ct)
    {
        var user = await _users.FindSingleAsync(
            u => u.CompanyId == req.CompanyId && u.Username == req.Username, ct);
        if (user is null || !user.HasRecoveryCode)
            return Result.Failure(GenericError);

        if (!PasswordHasher.Verify(req.RecoveryCode, user.RecoveryCodeHash!, user.RecoveryCodeSalt!))
            return Result.Failure(GenericError);

        var pw = SamaHesab.Application.Common.Validation.PasswordPolicy.Validate(req.NewPassword);
        if (!pw.Ok) return Result.Failure(pw.Error!);

        var (hash, salt) = PasswordHasher.Create(req.NewPassword);
        user.SetPassword(hash, salt);
        if (user.IsLocked) user.Unlock();
        _users.Update(user);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
