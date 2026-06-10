using SamaHesab.Application.Accounting;

namespace SamaHesab.Application.Automation;

public enum AlertSeverity { Info, Warning, Critical }

/// <summary>یک اعلان عملیاتی برای کاربر/داشبورد.</summary>
public record Alert(string Kind, AlertSeverity Severity, string Title, int? RefId, decimal Amount = 0);

/// <summary>ورودی چک برای ارزیابی اعلان.</summary>
public record ChequeAlertInput(int Id, string ChequeNumber, string DueDate, decimal Amount, string PartyType);

/// <summary>ورودی موجودی کالا برای ارزیابی اعلان.</summary>
public record StockAlertInput(int ProductId, string Name, decimal OnHand, decimal MinStock, decimal? ReorderPoint);

/// <summary>
/// موتور تولید اعلان‌های اتوماسیون — منطق خالص و تست‌پذیر (بدون دسترسی به داده).
/// منبعِ اعلان‌ها: سررسید چک، کسری موجودی.
/// </summary>
public static class AlertEngine
{
    /// <summary>اعلان چک‌های در جریان: سررسیدگذشته=بحرانی، امروز=هشدار.</summary>
    public static IEnumerable<Alert> ChequeAlerts(IEnumerable<ChequeAlertInput> cheques, string today)
    {
        foreach (var c in cheques)
        {
            var state = ChequeBoard.Classify(c.DueDate, today);
            if (state == ChequeDueState.Overdue)
                yield return new Alert("ChequeOverdue", AlertSeverity.Critical,
                    $"چک سررسیدگذشته #{c.ChequeNumber}", c.Id, c.Amount);
            else if (state == ChequeDueState.DueToday)
                yield return new Alert("ChequeDueToday", AlertSeverity.Warning,
                    $"چک سررسید امروز #{c.ChequeNumber}", c.Id, c.Amount);
        }
    }

    /// <summary>
    /// اعلان کسری موجودی: آستانه = ReorderPoint (یا MinStock اگر نقطه‌ی سفارش تعریف نشده).
    /// موجودی صفر/منفی=بحرانی، زیر آستانه=هشدار. آستانه‌ی صفر نادیده گرفته می‌شود.
    /// </summary>
    public static IEnumerable<Alert> LowStockAlerts(IEnumerable<StockAlertInput> stock)
    {
        foreach (var s in stock)
        {
            var threshold = (s.ReorderPoint is > 0) ? s.ReorderPoint!.Value : s.MinStock;
            if (threshold <= 0) continue;
            if (s.OnHand <= 0)
                yield return new Alert("OutOfStock", AlertSeverity.Critical,
                    $"اتمام موجودی: {s.Name}", s.ProductId, s.OnHand);
            else if (s.OnHand <= threshold)
                yield return new Alert("LowStock", AlertSeverity.Warning,
                    $"کسری موجودی: {s.Name} ({s.OnHand} ≤ {threshold})", s.ProductId, s.OnHand);
        }
    }

    /// <summary>همه‌ی اعلان‌ها، مرتب بر شدت (بحرانی اول).</summary>
    public static List<Alert> Build(IEnumerable<ChequeAlertInput> cheques, string today,
        IEnumerable<StockAlertInput> stock)
        => ChequeAlerts(cheques, today)
            .Concat(LowStockAlerts(stock))
            .OrderByDescending(a => a.Severity)
            .ToList();
}
