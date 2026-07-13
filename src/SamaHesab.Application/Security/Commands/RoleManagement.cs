using FluentValidation;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Application.Common.Security;
using SamaHesab.Domain.Entities.Security;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Security.Commands;

// ── کاتالوگ مجوزها (برای ساخت UI تخصیص) ───────────────────────────────────────
public record GetPermissionCatalogQuery() : IRequest<List<PermissionDef>>;
public class GetPermissionCatalogQueryHandler : IRequestHandler<GetPermissionCatalogQuery, List<PermissionDef>>
{
    public Task<List<PermissionDef>> Handle(GetPermissionCatalogQuery req, CancellationToken ct)
        => Task.FromResult(PermissionCatalog.All.ToList());
}

// ── فهرست نقش‌ها (با مجوزها) ───────────────────────────────────────────────────
public record GetRolesQuery() : IRequest<List<RoleDto>>;
public record RoleDto(int Id, string Code, string Name, bool IsSystem, bool IsActive, string[] Permissions);

public class GetRolesQueryHandler : IRequestHandler<GetRolesQuery, List<RoleDto>>
{
    private readonly IRepository<Role> _roles;
    private readonly IRepository<RolePermission> _perms;
    public GetRolesQueryHandler(IRepository<Role> roles, IRepository<RolePermission> perms)
    { _roles = roles; _perms = perms; }

    public async Task<List<RoleDto>> Handle(GetRolesQuery req, CancellationToken ct)
    {
        var roles = await _roles.GetAllAsync(ct);
        var allPerms = await _perms.GetAllAsync(ct);
        return roles.OrderBy(r => r.Code).Select(r => new RoleDto(
            r.Id, r.Code, r.Name, r.IsSystem, r.IsActive,
            allPerms.Where(p => p.RoleId == r.Id).Select(p => p.PermissionCode).ToArray())).ToList();
    }
}

// ── ذخیرهٔ نقش (ایجاد/ویرایش نام) ──────────────────────────────────────────────
public record SaveRoleCommand(int Id, string Code, string Name) : IRequest<Result<int>>;

public class SaveRoleCommandValidator : AbstractValidator<SaveRoleCommand>
{
    public SaveRoleCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().WithMessage("کد نقش الزامی است.");
        RuleFor(x => x.Name).NotEmpty().WithMessage("نام نقش الزامی است.");
    }
}

public class SaveRoleCommandHandler : IRequestHandler<SaveRoleCommand, Result<int>>
{
    private readonly IRepository<Role> _roles;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;
    public SaveRoleCommandHandler(IRepository<Role> roles, IUnitOfWork uow, ICurrentUserService user)
    { _roles = roles; _uow = uow; _user = user; }

    public async Task<Result<int>> Handle(SaveRoleCommand req, CancellationToken ct)
    {
        try
        {
            if (req.Id == 0)
            {
                if (await _roles.AnyAsync(r => r.Code == req.Code, ct))
                    return Result<int>.Failure("کد نقش تکراری است.");
                var role = Role.Create(_user.CompanyId ?? 1, req.Code, req.Name);
                await _roles.AddAsync(role, ct);
                await _uow.SaveChangesAsync(ct);
                return Result<int>.Success(role.Id);
            }
            var existing = await _roles.GetByIdAsync(req.Id, ct);
            if (existing is null) return Result<int>.Failure("نقش یافت نشد.");
            existing.Rename(req.Name);
            _roles.Update(existing);
            await _uow.SaveChangesAsync(ct);
            return Result<int>.Success(existing.Id);
        }
        catch (Exception ex) { return Result<int>.Failure(ex.GetBaseException().Message); }
    }
}

// ── جایگزینیِ مجوزهای یک نقش ────────────────────────────────────────────────────
public record SetRolePermissionsCommand(int RoleId, string[] Codes) : IRequest<Result>;

public class SetRolePermissionsCommandHandler : IRequestHandler<SetRolePermissionsCommand, Result>
{
    private readonly IRepository<Role> _roles;
    private readonly IRepository<RolePermission> _perms;
    private readonly IUnitOfWork _uow;
    public SetRolePermissionsCommandHandler(IRepository<Role> roles, IRepository<RolePermission> perms, IUnitOfWork uow)
    { _roles = roles; _perms = perms; _uow = uow; }

    public async Task<Result> Handle(SetRolePermissionsCommand req, CancellationToken ct)
    {
        var role = await _roles.GetByIdAsync(req.RoleId, ct);
        if (role is null) return Result.Failure("نقش یافت نشد.");

        var existing = await _perms.FindAsync(p => p.RoleId == req.RoleId, ct);
        _perms.RemoveRange(existing);
        var valid = new HashSet<string>(PermissionCatalog.All.Select(p => p.Code)) { PermissionCatalog.Wildcard };
        foreach (var code in req.Codes.Distinct().Where(valid.Contains))
            await _perms.AddAsync(RolePermission.Create(req.RoleId, code), ct);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}

// ── کاربران + نقش‌هایشان ────────────────────────────────────────────────────────
public record GetSecurityUsersQuery() : IRequest<List<SecurityUserDto>>;
public record SecurityUserDto(int Id, string Username, string FullName, bool IsActive, int[] RoleIds,
    int? SalespersonPartyId = null);

public class GetSecurityUsersQueryHandler : IRequestHandler<GetSecurityUsersQuery, List<SecurityUserDto>>
{
    private readonly IRepository<User> _users;
    private readonly IRepository<UserRole> _userRoles;
    public GetSecurityUsersQueryHandler(IRepository<User> users, IRepository<UserRole> userRoles)
    { _users = users; _userRoles = userRoles; }

    public async Task<List<SecurityUserDto>> Handle(GetSecurityUsersQuery req, CancellationToken ct)
    {
        var users = await _users.GetAllAsync(ct);
        var links = await _userRoles.GetAllAsync(ct);
        return users.OrderBy(u => u.Username).Select(u => new SecurityUserDto(
            u.Id, u.Username, u.FullName, u.IsActive,
            links.Where(l => l.UserId == u.Id).Select(l => l.RoleId).ToArray(),
            u.SalespersonPartyId)).ToList();
    }
}

// ── SP-1: تخصیصِ «فروشنده» به کاربر (پنلِ فروشِ گردشگریِ فروشنده‌محور) ───────────────
public record SetUserSalespersonCommand(int UserId, int? SalespersonPartyId) : IRequest<Result>;

public class SetUserSalespersonCommandHandler : IRequestHandler<SetUserSalespersonCommand, Result>
{
    private readonly IRepository<User> _users;
    private readonly IUnitOfWork _uow;
    public SetUserSalespersonCommandHandler(IRepository<User> users, IUnitOfWork uow)
    { _users = users; _uow = uow; }

    public async Task<Result> Handle(SetUserSalespersonCommand req, CancellationToken ct)
    {
        var user = await _users.GetByIdAsync(req.UserId, ct);
        if (user is null) return Result.Failure("کاربر یافت نشد.");
        user.SetSalesperson(req.SalespersonPartyId);
        _users.Update(user);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}

// ── جایگزینیِ نقش‌های یک کاربر ──────────────────────────────────────────────────
public record SetUserRolesCommand(int UserId, int[] RoleIds) : IRequest<Result>;

public class SetUserRolesCommandHandler : IRequestHandler<SetUserRolesCommand, Result>
{
    private readonly IRepository<UserRole> _userRoles;
    private readonly IRepository<Role> _roles;
    private readonly IUnitOfWork _uow;
    public SetUserRolesCommandHandler(IRepository<UserRole> userRoles, IRepository<Role> roles, IUnitOfWork uow)
    { _userRoles = userRoles; _roles = roles; _uow = uow; }

    public async Task<Result> Handle(SetUserRolesCommand req, CancellationToken ct)
    {
        // U-SEC-8 — پیش‌تر هیچ گاردی نبود: یک ادمین می‌توانست نقشِ ADMIN را از تنها کاربرِ ادمینِ
        // سیستم (حتی از خودش) حذف کند و کلِ مدیریتِ امنیت/نقش را برایِ همیشه قفل کند (فقط با دستکاریِ
        // مستقیمِ DB قابلِ‌ترمیم). محدودهٔ عمدی: فقط نقشِ literal با Code=="ADMIN" را می‌گیرد؛ نقشِ
        // سفارشی با مجوزِ wildcard («*») پوشش داده نمی‌شود (کارِ بازِ مستندشده در todo.rm).
        var adminRole = await _roles.FindSingleAsync(r => r.Code == "ADMIN", ct);
        if (adminRole != null && !req.RoleIds.Contains(adminRole.Id))
        {
            var wasAdmin = await _userRoles.AnyAsync(ur => ur.UserId == req.UserId && ur.RoleId == adminRole.Id, ct);
            if (wasAdmin)
            {
                var otherAdminExists = await _userRoles.AnyAsync(ur => ur.RoleId == adminRole.Id && ur.UserId != req.UserId, ct);
                if (!otherAdminExists)
                    return Result.Failure("این تنها کاربرِ دارایِ نقشِ ادمین است؛ نمی‌توانید این نقش را از او بگیرید (سیستم برایِ همیشه قفل می‌شود).");
            }
        }

        var existing = await _userRoles.FindAsync(ur => ur.UserId == req.UserId, ct);
        _userRoles.RemoveRange(existing);
        foreach (var roleId in req.RoleIds.Distinct())
            await _userRoles.AddAsync(UserRole.Create(req.UserId, roleId), ct);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
