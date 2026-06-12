using SamaHesab.Domain.Common;

namespace SamaHesab.Domain.Entities.Security;

/// <summary>
/// نقش امنیتی (RBAC). مجموعه‌ای از مجوزها (کدِ «Module.Feature.Action») که به کاربران تخصیص می‌یابد.
/// نقش‌های سیستمی (IsSystem) قابل حذف نیستند.
/// </summary>
public class Role : AuditableEntity
{
    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    public bool IsSystem { get; private set; }
    public bool IsActive { get; private set; } = true;

    public ICollection<RolePermission> Permissions { get; private set; } = new List<RolePermission>();

    private Role() { }

    public static Role Create(int companyId, string code, string name, bool isSystem = false)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("کد نقش الزامی است.");
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("نام نقش الزامی است.");
        return new Role { CompanyId = companyId, Code = code, Name = name, IsSystem = isSystem };
    }

    public void Rename(string name) { Name = name; SetAudit(null); }
    public void Deactivate() { IsActive = false; SetAudit(null); }
    public void Activate() { IsActive = true; SetAudit(null); }
}

/// <summary>یک مجوزِ اعطاشده به نقش (کدِ «Module.Feature.Action» یا «Module.*»).</summary>
public class RolePermission : BaseEntity
{
    public int RoleId { get; private set; }
    public string PermissionCode { get; private set; } = default!;

    private RolePermission() { }

    public static RolePermission Create(int roleId, string permissionCode)
        => new() { RoleId = roleId, PermissionCode = permissionCode };
}

/// <summary>تخصیص نقش به کاربر.</summary>
public class UserRole : BaseEntity
{
    public int UserId { get; private set; }
    public int RoleId { get; private set; }

    private UserRole() { }

    public static UserRole Create(int userId, int roleId)
        => new() { UserId = userId, RoleId = roleId };
}
