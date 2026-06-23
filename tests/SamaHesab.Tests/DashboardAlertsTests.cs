using System.Linq;
using SamaHesab.Application.Reports;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>هشدارهای قابل‌اقدامِ داشبورد — فقط ناصفرها، مرتب بر شدت سپس مبلغ.</summary>
public class DashboardAlertsTests
{
    [Fact]
    public void Only_NonZero_Metrics_Produce_Alerts()
    {
        var alerts = DashboardAlerts.Build(new DashboardAlertsInput(
            OverdueChequeCount: 2, OverdueChequeAmount: 500,
            DueSoonChequeCount: 0));   // صفر → هیچ هشداری

        var a = Assert.Single(alerts);
        Assert.Equal("cheque-overdue", a.Key);
        Assert.Equal(AlertSeverity.Critical, a.Severity);
        Assert.Equal("cheque-board", a.NavTarget);
    }

    [Fact]
    public void Empty_Input_Yields_No_Alerts()
        => Assert.Empty(DashboardAlerts.Build(new DashboardAlertsInput()));

    [Fact]
    public void Critical_Sorts_Before_Warning()
    {
        var alerts = DashboardAlerts.Build(new DashboardAlertsInput(
            DueSoonChequeCount: 5, DueSoonChequeAmount: 9999,   // Warning
            OverdueChequeCount: 1, OverdueChequeAmount: 10));   // Critical

        Assert.Equal("cheque-overdue", alerts[0].Key);   // بحرانی اول، با وجودِ مبلغِ کمتر
        Assert.Equal("cheque-due-soon", alerts[1].Key);
    }

    [Fact]
    public void Within_Same_Severity_Higher_Amount_First()
    {
        var alerts = DashboardAlerts.Build(new DashboardAlertsInput(
            OverdueChequeCount: 1, OverdueChequeAmount: 100,
            OverdueReceivableCount: 1, OverdueReceivableAmount: 900));

        Assert.Equal("receivable-overdue", alerts[0].Key);   // مبلغِ بیشتر اول
        Assert.Equal("cheque-overdue", alerts[1].Key);
    }
}
