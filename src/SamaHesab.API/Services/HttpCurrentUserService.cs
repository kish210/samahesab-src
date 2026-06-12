using System.Security.Claims;
using SamaHesab.Application.Common.Interfaces;

namespace SamaHesab.API.Services;

/// <summary>
/// API-side implementation of <see cref="ICurrentUserService"/> that reads the
/// authenticated principal from the current HTTP request (JWT claims).
/// The same Application-layer handlers used by the WPF client therefore work
/// unchanged behind the Web API.
/// </summary>
public class HttpCurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _accessor;

    public HttpCurrentUserService(IHttpContextAccessor accessor) => _accessor = accessor;

    private ClaimsPrincipal? User => _accessor.HttpContext?.User;

    private int? GetInt(string claim) =>
        int.TryParse(User?.FindFirst(claim)?.Value, out var v) ? v : (int?)null;

    public int? UserId => GetInt("uid");
    public int? CompanyId => GetInt("companyId");
    public int? BranchId => GetInt("branchId");
    public string? Username => User?.Identity?.Name ?? User?.FindFirst("username")?.Value;
    public string? FullName => User?.FindFirst("fullName")?.Value;
    public bool IsAuthenticated => User?.Identity?.IsAuthenticated ?? false;

    public bool HasPermission(string moduleCode, string featureCode, string action)
    {
        if (GetRoles().Contains("ADMIN")) return true;
        var granted = User?.FindAll("perm").Select(c => c.Value) ?? Enumerable.Empty<string>();
        var key = string.Join('.', new[] { moduleCode, featureCode, action }
            .Where(s => !string.IsNullOrEmpty(s)));
        return SamaHesab.Application.Common.Security.PermissionCatalog.Grants(granted, key);
    }

    public IEnumerable<string> GetRoles() =>
        User?.FindAll(ClaimTypes.Role).Select(c => c.Value) ?? Enumerable.Empty<string>();
}
