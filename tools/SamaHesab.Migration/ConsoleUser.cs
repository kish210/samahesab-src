using SamaHesab.Application.Common.Interfaces;

namespace SamaHesab.Migration;

/// <summary>کاربرِ جاریِ ابزارِ مهاجرت (ادمینِ شرکتِ ۱) — تا RBAC/حسابرسیِ کامندهای import مانع نشوند.</summary>
internal sealed class ConsoleUser : ICurrentUserService
{
    public int? UserId => 1;
    public int? CompanyId => 1;
    public int? BranchId => 1;
    public string? Username => "migration";
    public string? FullName => "ابزارِ مهاجرت";
    public bool IsAuthenticated => true;
    public bool HasPermission(string moduleCode, string featureCode, string action) => true;
    public IEnumerable<string> GetRoles() => new[] { "ADMIN" };
}
