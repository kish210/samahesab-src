using SamaHesab.Application.Common.Interfaces;

namespace SamaHesab.WPF.Services;

public class CurrentUserService : ICurrentUserService
{
    private int? _userId;
    private int? _companyId;
    private int? _branchId;
    private string? _username;
    private string? _fullName;
    private int? _salespersonPartyId;
    private List<string> _roles = new();
    private HashSet<string> _permissions = new();

    public int? UserId => _userId;
    public int? CompanyId => _companyId;
    public int? BranchId => _branchId;
    public string? Username => _username;
    public string? FullName => _fullName;
    public int? SalespersonPartyId => _salespersonPartyId;
    public bool IsAuthenticated => _userId.HasValue;

    public void SetCurrentUser(int userId, int companyId, int? branchId,
        string username, string fullName,
        IEnumerable<string> roles, IEnumerable<string> permissions, int? salespersonPartyId = null)
    {
        _userId = userId;
        _companyId = companyId;
        _branchId = branchId;
        _username = username;
        _fullName = fullName;
        _salespersonPartyId = salespersonPartyId;
        _roles = roles.ToList();
        _permissions = new HashSet<string>(permissions);
    }

    public void Clear()
    {
        _userId = null;
        _companyId = null;
        _branchId = null;
        _username = null;
        _fullName = null;
        _salespersonPartyId = null;
        _roles.Clear();
        _permissions.Clear();
    }

    public bool HasPermission(string moduleCode, string featureCode, string action)
    {
        if (_roles.Contains("ADMIN")) return true;
        // کلید از بخش‌های غیرخالی ساخته می‌شود تا کدهای ۲ و ۳ بخشیِ کاتالوگ سازگار بمانند.
        var key = string.Join('.', new[] { moduleCode, featureCode, action }
            .Where(s => !string.IsNullOrEmpty(s)));
        // پشتیبانی از «*» و wildcardِ ماژول (مثل «Treasury.*») از طریق کاتالوگ مجوز.
        return SamaHesab.Application.Common.Security.PermissionCatalog.Grants(_permissions, key);
    }

    public IEnumerable<string> GetRoles() => _roles;
}
