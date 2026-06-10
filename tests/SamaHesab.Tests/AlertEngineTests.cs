using SamaHesab.Application.Automation;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>موتور اعلان‌های اتوماسیون — سررسید چک + کسری موجودی.</summary>
public class AlertEngineTests
{
    [Fact]
    public void ChequeAlerts_Flags_Overdue_And_DueToday_Only()
    {
        var cheques = new[]
        {
            new ChequeAlertInput(1, "C1", "1404/03/01", 1000, "Received"), // گذشته
            new ChequeAlertInput(2, "C2", "1404/03/10", 2000, "Received"), // امروز
            new ChequeAlertInput(3, "C3", "1404/03/20", 3000, "Received"), // پیش رو → بدون اعلان
        };
        var alerts = AlertEngine.ChequeAlerts(cheques, "1404/03/10").ToList();
        Assert.Equal(2, alerts.Count);
        Assert.Equal(AlertSeverity.Critical, alerts[0].Severity);
        Assert.Equal("ChequeOverdue", alerts[0].Kind);
        Assert.Equal("ChequeDueToday", alerts[1].Kind);
    }

    [Fact]
    public void LowStock_Uses_ReorderPoint_Then_MinStock()
    {
        var stock = new[]
        {
            new StockAlertInput(10, "روغن", OnHand: 5,  MinStock: 3, ReorderPoint: 8),  // 5<=8 → هشدار
            new StockAlertInput(20, "برنج", OnHand: 0,  MinStock: 2, ReorderPoint: null), // اتمام → بحرانی
            new StockAlertInput(30, "نمک", OnHand: 50, MinStock: 5, ReorderPoint: 10),   // کافی → هیچ
            new StockAlertInput(40, "شکر", OnHand: 1,  MinStock: 0, ReorderPoint: null),  // آستانه‌ی صفر → نادیده
        };
        var alerts = AlertEngine.LowStockAlerts(stock).ToList();
        Assert.Equal(2, alerts.Count);
        Assert.Contains(alerts, a => a.Kind == "LowStock" && a.RefId == 10);
        Assert.Contains(alerts, a => a.Kind == "OutOfStock" && a.RefId == 20);
        Assert.DoesNotContain(alerts, a => a.RefId == 30 || a.RefId == 40);
    }

    [Fact]
    public void Build_Sorts_Critical_First()
    {
        var cheques = new[] { new ChequeAlertInput(1, "C1", "1404/03/01", 1000, "Received") };
        var stock = new[] { new StockAlertInput(10, "روغن", 5, 3, 8) };
        var all = AlertEngine.Build(cheques, "1404/03/10", stock);
        Assert.Equal(2, all.Count);
        Assert.Equal(AlertSeverity.Critical, all[0].Severity);
        Assert.Equal(AlertSeverity.Warning, all[1].Severity);
    }
}
