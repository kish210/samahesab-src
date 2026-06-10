namespace SamaHesab.Application.BI;

/// <summary>یک حرکت موجودی (کاردکس). Quantity مثبت=ورود، منفی=خروج.</summary>
public record StockMovement(string Date, decimal Quantity);

/// <summary>یک نقطه از روند موجودی در یک ماه.</summary>
public record InventoryTrendPoint(string Period, decimal InQty, decimal OutQty, decimal Net);

/// <summary>
/// موتور روند موجودی — منطق خالص و تست‌پذیر.
/// تجمیع ماهانه‌ی ورود/خروج از کاردکس (تاریخ شمسی yyyy/MM/dd).
/// </summary>
public static class InventoryAnalytics
{
    public static List<InventoryTrendPoint> MonthlyMovement(IEnumerable<StockMovement> moves)
        => moves
            .GroupBy(m => SalesAnalytics.MonthKey(m.Date))
            .Where(g => g.Key.Length > 0)
            .Select(g =>
            {
                var inQty = g.Where(x => x.Quantity > 0).Sum(x => x.Quantity);
                var outQty = g.Where(x => x.Quantity < 0).Sum(x => -x.Quantity);
                return new InventoryTrendPoint(g.Key, inQty, outQty, inQty - outQty);
            })
            .OrderBy(p => p.Period)
            .ToList();
}
