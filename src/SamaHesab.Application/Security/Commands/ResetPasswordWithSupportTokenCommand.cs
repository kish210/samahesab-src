using MediatR;
using SamaHesab.Application.Common.Models;
using SamaHesab.Application.Common.Security;
using SamaHesab.Application.Common.Validation;
using SamaHesab.Application.Licensing;
using SamaHesab.Domain.Entities.Security;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Security.Commands;

/// <summary>
/// U-SUPPORT-RESET — مسیرِ دومِ بازیابیِ رمز (کنارِ ResetPasswordWithRecoveryCodeCommandِ موجود):
/// وقتی کاربر کدِ بازیابیِ محلی را هم گم کرده، پشتیبانی با ابزارِ آفلاینِ خودش (کلیدِ خصوصی، هرگز
/// در کلاینت/این مخزن نیست) یک توکنِ کوتاه‌مدتِ مخصوصِ همین Fingerprintِ ماشین امضا می‌کند.
/// </summary>
public record ResetPasswordWithSupportTokenCommand(int CompanyId, string Username, string TokenCode, string NewPassword)
    : IRequest<Result>;

public class ResetPasswordWithSupportTokenCommandHandler : IRequestHandler<ResetPasswordWithSupportTokenCommand, Result>
{
    private readonly IRepository<User> _users;
    private readonly IUnitOfWork _uow;
    private readonly IMachineFingerprintProvider _fingerprint;

    public ResetPasswordWithSupportTokenCommandHandler(IRepository<User> users, IUnitOfWork uow, IMachineFingerprintProvider fingerprint)
    { _users = users; _uow = uow; _fingerprint = fingerprint; }

    public async Task<Result> Handle(ResetPasswordWithSupportTokenCommand req, CancellationToken ct)
    {
        var doc = SupportResetTokenDocument.FromCode(req.TokenCode);
        var ok = SupportResetTokenSigner.Verify(doc, _fingerprint.GetFingerprint(), DateTime.UtcNow, LicensePublicKey.Pem);
        if (!ok) return Result.Failure("کدِ پشتیبانی نامعتبر، منقضی‌شده، یا مخصوصِ دستگاهِ دیگری است.");

        var (valid, err) = PasswordPolicy.Validate(req.NewPassword);
        if (!valid) return Result.Failure(err!);

        var user = await _users.FindSingleAsync(u => u.CompanyId == req.CompanyId && u.Username == req.Username, ct);
        if (user is null) return Result.Failure("کاربری با این نامِ کاربری در این شرکت یافت نشد.");

        var (hash, salt) = PasswordHasher.Create(req.NewPassword);
        user.SetPassword(hash, salt);
        if (user.IsLocked) user.Unlock();
        _users.Update(user);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
