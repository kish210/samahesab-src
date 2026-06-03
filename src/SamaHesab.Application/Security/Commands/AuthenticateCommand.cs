using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Application.Common.Security;
using SamaHesab.Domain.Entities.Security;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Security.Commands;

public record AuthResult(int UserId, int CompanyId, int BranchId, string Username, string FullName, string[] Roles);

public record AuthenticateCommand(int CompanyId, string Username, string Password, string? IpAddress = null)
    : IRequest<Result<AuthResult>>;

public class AuthenticateCommandHandler : IRequestHandler<AuthenticateCommand, Result<AuthResult>>
{
    private readonly IRepository<User> _users;
    private readonly IRepository<AuditLog> _audit;
    private readonly IUnitOfWork _uow;

    public AuthenticateCommandHandler(IRepository<User> users, IRepository<AuditLog> audit, IUnitOfWork uow)
    { _users = users; _audit = audit; _uow = uow; }

    public async Task<Result<AuthResult>> Handle(AuthenticateCommand req, CancellationToken ct)
    {
        var user = await _users.FindSingleAsync(
            u => u.CompanyId == req.CompanyId && u.Username == req.Username, ct);

        if (user == null)
            return Result<AuthResult>.Failure("نام کاربری یا رمز عبور نادرست است.");
        if (!user.IsActive)
            return Result<AuthResult>.Failure("حساب کاربری غیرفعال است.");
        if (user.IsLocked && (user.LockoutEnd == null || user.LockoutEnd > DateTime.Now))
            return Result<AuthResult>.Failure("حساب کاربری به دلیل تلاش‌های ناموفق قفل شده است.");

        if (!PasswordHasher.Verify(req.Password, user.PasswordHash, user.PasswordSalt))
        {
            user.RecordFailedAttempt();
            _users.Update(user);
            await _audit.AddAsync(AuditLog.Create("LoginFailed", user.Id, user.Username,
                "Sec.Users", user.Id.ToString(), null, req.IpAddress), ct);
            await _uow.SaveChangesAsync(ct);
            return Result<AuthResult>.Failure("نام کاربری یا رمز عبور نادرست است.");
        }

        user.RecordSuccessfulLogin();
        _users.Update(user);
        await _audit.AddAsync(AuditLog.Create("Login", user.Id, user.Username,
            "Sec.Users", user.Id.ToString(), null, req.IpAddress), ct);
        await _uow.SaveChangesAsync(ct);

        // Role mapping is simplified for now; the seeded admin gets the ADMIN role.
        var roles = user.Username == "admin" ? new[] { "ADMIN" } : Array.Empty<string>();
        return Result<AuthResult>.Success(new AuthResult(
            user.Id, user.CompanyId, user.BranchId ?? 1, user.Username, user.FullName, roles));
    }
}
