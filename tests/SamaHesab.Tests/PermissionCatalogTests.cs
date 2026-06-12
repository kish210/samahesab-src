using SamaHesab.Application.Common.Security;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>
/// تستِ منطقِ مجوز RBAC — `PermissionCatalog.Grants` (متد static).
/// (ایدهٔ اولیه از Cline/qwen بود؛ به‌دلیل خطاهای کامپایل توسط کلود ۱ بازنویسی شد:
///  PermissionCatalog کلاس static است و متدِ توهمیِ GrNone وجود ندارد.)
/// </summary>
public class PermissionCatalogTests
{
    [Fact]
    public void Grants_ExactMatch()
        => Assert.True(PermissionCatalog.Grants(new[] { "Accounting.Voucher.View" }, "Accounting.Voucher.View"));

    [Fact]
    public void Grants_MissingPermission()
        => Assert.False(PermissionCatalog.Grants(new[] { "Reports.View" }, "Accounting.Voucher.View"));

    [Fact]
    public void Grants_WildcardAll_GrantsEverything()
    {
        Assert.True(PermissionCatalog.Grants(new[] { "*" }, "Accounting.Voucher.View"));
        Assert.True(PermissionCatalog.Grants(new[] { "*" }, "Sales.Invoice.View"));
    }

    [Fact]
    public void Grants_ModuleWildcard_ScopesToModule()
    {
        Assert.True(PermissionCatalog.Grants(new[] { "Treasury.*" }, "Treasury.View"));
        Assert.False(PermissionCatalog.Grants(new[] { "Treasury.*" }, "Sales.Invoice.View"));
    }

    [Fact]
    public void Grants_Empty_GrantsNothing()
        => Assert.False(PermissionCatalog.Grants(System.Array.Empty<string>(), "Accounting.Voucher.View"));
}
