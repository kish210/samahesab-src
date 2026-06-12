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
public record SecurityUserDto(int Id, string Username, string FullName, bool IsActive, int[] RoleIds);

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
            links.Where(l => l.UserId == u.Id).Select(l => l.RoleId).ToArray())).ToList();
    }
}

// ── جایگزینیِ نقش‌های یک کاربر ──────────────────────────────────────────────────
public record SetUserRolesCommand(int UserId, int[] RoleIds) : IRequest<Result>;

public class SetUserRolesCommandHandler : IRequestHandler<SetUserRolesCommand, Result>
{
    private readonly IRepository<UserRole> _userRoles;
    private readonly IUnitOfWork _uow;
    public SetUserRolesCommandHandler(IRepository<UserRole> userRoles, IUnitOfWork uow)
    { _userRoles = userRoles; _uow = uow; }

    public async Task<Result> Handle(SetUserRolesCommand req, CancellationToken ct)
    {
        var existing = await _userRoles.FindAsync(ur => ur.UserId == req.UserId, ct);
        _userRoles.RemoveRange(existing);
        foreach (var roleId in req.RoleIds.Distinct())
            await _userRoles.AddAsync(UserRole.Create(req.UserId, roleId), ct);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
