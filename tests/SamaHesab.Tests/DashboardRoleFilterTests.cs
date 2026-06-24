using System.Linq;
using SamaHesab.Application.Reports;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>فیلترِ نقش‌محورِ هشدارهای داشبورد — «کارهای امروزِ من».</summary>
public class DashboardRoleFilterTests
{
    private static readonly System.Collections.Generic.List<ActionableAlert> All = new()
    {
        new(AlertSeverity.Critical, "cheque-overdue",     "چک", 1, 100, "cheque-board"),
        new(AlertSeverity.Critical, "receivable-overdue", "دریافتنی", 1, 200, "party-aging"),
        new(AlertSeverity.Warning,  "stock-low",          "کسری", 3, 0, "inventory-overview"),
        new(AlertSeverity.Warning,  "tourism-deposit-low","ودیعه", 2, 0, "tourism-deposits"),
        new(AlertSeverity.Warning,  "guarantee-expiring", "ضمانت", 1, 0, "contracting-dashboard"),
    };

    [Fact]
    public void Manager_Sees_All()
        => Assert.Equal(All.Count, DashboardRoleFilter.For(DashboardRole.Manager, All).Count);

    [Fact]
    public void Accountant_Sees_Cheque_And_Receivable_Only()
    {
        var keys = DashboardRoleFilter.For(DashboardRole.Accountant, All).Select(a => a.Key).ToList();
        Assert.Contains("cheque-overdue", keys);
        Assert.Contains("receivable-overdue", keys);
        Assert.DoesNotContain("stock-low", keys);
        Assert.DoesNotContain("tourism-deposit-low", keys);
    }

    [Fact]
    public void InventoryManager_Sees_Only_Stock()
    {
        var f = DashboardRoleFilter.For(DashboardRole.InventoryManager, All);
        Assert.Single(f);
        Assert.Equal("stock-low", f[0].Key);
    }

    [Fact]
    public void Tourism_And_Project_Roles_Scoped()
    {
        Assert.Equal("tourism-deposit-low", DashboardRoleFilter.For(DashboardRole.TourismOperator, All).Single().Key);
        Assert.Equal("guarantee-expiring", DashboardRoleFilter.For(DashboardRole.ProjectManager, All).Single().Key);
    }

    [Fact]
    public void Preserves_Input_Order()
    {
        var f = DashboardRoleFilter.For(DashboardRole.Manager, All);
        Assert.Equal("cheque-overdue", f[0].Key);
        Assert.Equal("guarantee-expiring", f[^1].Key);
    }
}
