using SamaHesab.Application.Accounting;

namespace SamaHesab.Application.Automation;

public enum AlertSeverity { Info, Warning, Critical }

/// <summary>یک اعلان عملیاتی برای کاربر/داشبورد.</summary>
public record Alert(string Kind, AlertSeverity Severity, string Title, int? RefId, decimal Amount = 0);

/// <summary>ورودی چک برای ارزیابی اعلان.</summary>
public record ChequeAlertInput(int Id, string ChequeNumber, string DueDate, decimal Amount, string PartyType);

/// <summary>ورودی موجودی کالا برای ارزیابی اعلان.</summary>
public record StockAlertInput(int ProductId, string Name, decimal OnHand, decimal MinStock, decimal? ReorderPoint);

/// <summary>ورودی فاکتور فروش معوق برای یادآور بدهی.</summary>
public record ReceivableAlertInput(int InvoiceId, string InvoiceNumber, string? DueDate, decimal Remain);

/// <summary>ورودی بچ کالا برای اعلان انقضا.</summary>
public record BatchAlertInput(int BatchId, string ProductName, string BatchNumber, string? ExpiryDate, decimal Quantity);

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

    /// <summary>یادآور بدهی: فاکتور فروشِ با ماندهٔ پرداخت که سررسیدش گذشته/امروز است.</summary>
    public static IEnumerable<Alert> DebtAlerts(IEnumerable<ReceivableAlertInput> invoices, string today)
    {
        foreach (var i in invoices)
        {
            if (i.Remain <= 0.01m || string.IsNullOrEmpty(i.DueDate)) continue;
            var cmp = string.CompareOrdinal(i.DueDate, today);
            if (cmp < 0)
                yield return new Alert("OverdueReceivable", AlertSeverity.Critical,
                    $"بدهی سررسیدگذشته فاکتور {i.InvoiceNumber}", i.InvoiceId, i.Remain);
            else if (cmp == 0)
                yield return new Alert("ReceivableDueToday", AlertSeverity.Warning,
                    $"سررسید بدهی فاکتور {i.InvoiceNumber} امروز است", i.InvoiceId, i.Remain);
        }
    }

    /// <summary>
    /// اعلان انقضا: بچِ دارای موجودی که منقضی شده (بحرانی) یا تا افق نزدیک منقضی می‌شود (هشدار).
    /// تاریخ‌ها شمسیِ yyyy/MM/dd؛ horizon را فراخواننده محاسبه می‌کند (مثلاً امروز + ۳۰ روز).
    /// </summary>
    public static IEnumerable<Alert> ExpiryAlerts(IEnumerable<BatchAlertInput> batches, string today, string horizon)
    {
        foreach (var b in batches)
        {
            if (b.Quantity <= 0 || string.IsNullOrEmpty(b.ExpiryDate)) continue;
            if (string.CompareOrdinal(b.ExpiryDate, today) < 0)
                yield return new Alert("Expired", AlertSeverity.Critical,
                    $"انقضای گذشته: {b.ProductName} (بچ {b.BatchNumber})", b.BatchId, b.Quantity);
            else if (string.CompareOrdinal(b.ExpiryDate, horizon) <= 0)
                yield return new Alert("ExpiringSoon", AlertSeverity.Warning,
                    $"انقضای نزدیک: {b.ProductName} (بچ {b.BatchNumber}, {b.ExpiryDate})", b.BatchId, b.Quantity);
        }
    }

    /// <summary>همه‌ی اعلان‌ها، مرتب بر شدت (بحرانی اول).</summary>
    public static List<Alert> Build(IEnumerable<ChequeAlertInput> cheques, string today,
        IEnumerable<StockAlertInput> stock)
        => ChequeAlerts(cheques, today)
            .Concat(LowStockAlerts(stock))
            .OrderByDescending(a => a.Severity)
            .ToList();

    /// <summary>مرتب‌سازی نهایی مجموعه‌ای از اعلان‌ها بر شدت (بحرانی اول).</summary>
    public static List<Alert> Sort(IEnumerable<Alert> alerts)
        => alerts.OrderByDescending(a => a.Severity).ToList();
}
