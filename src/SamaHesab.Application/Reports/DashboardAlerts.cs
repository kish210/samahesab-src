namespace SamaHesab.Application.Reports;

/// <summary>شدتِ هشدار — ترتیبِ نمایش از بحرانی به اطلاع.</summary>
public enum AlertSeverity { Critical = 0, Warning = 1, Info = 2 }

/// <summary>یک هشدارِ قابل‌اقدام در داشبورد: شدت + عنوان + تعداد/مبلغ + مقصدِ پرش.</summary>
public record ActionableAlert(AlertSeverity Severity, string Key, string Title,
    int Count, decimal Amount, string NavTarget);

/// <summary>سنجه‌های خامِ ورودیِ هشدارها (از کوئری‌های هر حوزه پر می‌شوند).</summary>
public record DashboardAlertsInput(
    int OverdueChequeCount = 0, decimal OverdueChequeAmount = 0,
    int DueSoonChequeCount = 0, decimal DueSoonChequeAmount = 0,
    int OverdueReceivableCount = 0, decimal OverdueReceivableAmount = 0,
    int LowStockCount = 0,
    int ExpiringGuaranteeCount = 0,
    int SupplierDepositLowCount = 0);

/// <summary>
/// سازندهٔ هشدارهای قابل‌اقدامِ داشبورد — منطقِ خالص و تست‌پذیر. رودمپ-تجربه:
/// «اتصالِ KPI به هشدارهای قابل‌اقدام (چکِ سررسید، کسری، دریافتنیِ معوق) با پرشِ مستقیم».
/// فقط سنجه‌های ناصفر هشدار می‌سازند؛ خروجی بر اساسِ شدت سپس مبلغ مرتب می‌شود.
/// </summary>
public static class DashboardAlerts
{
    /// <summary>
    /// از ردیف‌های ماندهٔ سنی‌شده، «معوق» را استخراج می‌کند: معوق = کل − جاری (هر چیزِ بالای ۳۰ روز).
    /// تعداد = طرف‌هایی با معوقِ مثبت · مبلغ = جمعِ معوق. منطقِ خالصِ تست‌پذیر.
    /// </summary>
    public static (int Count, decimal Amount) OverdueFromAging(IEnumerable<(decimal Current, decimal Total)> rows)
    {
        int count = 0; decimal amount = 0;
        foreach (var (current, total) in rows)
        {
            var overdue = total - current;
            if (overdue > 0.01m) { count++; amount += overdue; }
        }
        return (count, amount);
    }

    public static List<ActionableAlert> Build(DashboardAlertsInput m)
    {
        var list = new List<ActionableAlert>();

        if (m.OverdueChequeCount > 0)
            list.Add(new ActionableAlert(AlertSeverity.Critical, "cheque-overdue",
                "چک‌های سررسیدگذشته", m.OverdueChequeCount, m.OverdueChequeAmount, "cheque-board"));

        if (m.OverdueReceivableCount > 0)
            list.Add(new ActionableAlert(AlertSeverity.Critical, "receivable-overdue",
                "دریافتنیِ معوق", m.OverdueReceivableCount, m.OverdueReceivableAmount, "party-aging"));

        if (m.DueSoonChequeCount > 0)
            list.Add(new ActionableAlert(AlertSeverity.Warning, "cheque-due-soon",
                "چک‌های نزدیکِ سررسید (۷ روز)", m.DueSoonChequeCount, m.DueSoonChequeAmount, "cheque-board"));

        if (m.LowStockCount > 0)
            list.Add(new ActionableAlert(AlertSeverity.Warning, "stock-low",
                "کالاهای زیرِ حداقلِ موجودی", m.LowStockCount, 0, "inventory-overview"));

        if (m.SupplierDepositLowCount > 0)
            list.Add(new ActionableAlert(AlertSeverity.Warning, "tourism-deposit-low",
                "ودیعهٔ تأمین‌کنندهٔ رو به اتمام", m.SupplierDepositLowCount, 0, "tourism-deposits"));

        if (m.ExpiringGuaranteeCount > 0)
            list.Add(new ActionableAlert(AlertSeverity.Warning, "guarantee-expiring",
                "ضمانت‌نامه‌های روبه‌انقضا", m.ExpiringGuaranteeCount, 0, "contracting-dashboard"));

        return list
            .OrderBy(a => a.Severity)
            .ThenByDescending(a => a.Amount)
            .ThenByDescending(a => a.Count)
            .ToList();
    }
}
