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
    public void OverdueFromAging_Counts_And_Sums_Past_Current()
    {
        // (جاری، کل): معوق = کل − جاری
        var (count, amount) = DashboardAlerts.OverdueFromAging(new[]
        {
            (100m, 100m),   // همه جاری → معوق ۰ → شمرده نمی‌شود
            (50m, 200m),    // معوق ۱۵۰
            (0m, 80m),      // معوق ۸۰
            (10m, 10.005m), // معوقِ ناچیز (<۰٫۰۱) → نادیده
        });
        Assert.Equal(2, count);
        Assert.Equal(230m, amount);
    }

    [Fact]
    public void Overdue_Receivable_Produces_Critical_Alert()
    {
        var a = DashboardAlerts.Build(new DashboardAlertsInput(
            OverdueReceivableCount: 4, OverdueReceivableAmount: 5_000_000))
            .Single(x => x.Key == "receivable-overdue");
        Assert.Equal(AlertSeverity.Critical, a.Severity);
        Assert.Equal("party-aging", a.NavTarget);
        Assert.Equal(5_000_000, a.Amount);
    }

    [Fact]
    public void Low_Stock_Produces_Warning_Alert()
    {
        var a = DashboardAlerts.Build(new DashboardAlertsInput(LowStockCount: 6))
            .Single(x => x.Key == "stock-low");
        Assert.Equal(AlertSeverity.Warning, a.Severity);
        Assert.Equal("inventory-overview", a.NavTarget);
        Assert.Equal(6, a.Count);
    }

    [Fact]
    public void Tourism_Low_Deposit_Produces_Warning_Alert()
    {
        var a = Assert.Single(DashboardAlerts.Build(new DashboardAlertsInput(SupplierDepositLowCount: 3)));
        Assert.Equal("tourism-deposit-low", a.Key);
        Assert.Equal(AlertSeverity.Warning, a.Severity);
        Assert.Equal("tourism-deposits", a.NavTarget);
        Assert.Equal(3, a.Count);
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
