namespace SamaHesab.Application.Reports;

/// <summary>نرخِ گردش + روزِ ماندگاریِ موجودی.</summary>
public record TurnoverResult(decimal Ratio, decimal DaysOnHand);

/// <summary>
/// فاز ۱۲ (پولیش) — گردشِ موجودی: نرخِ گردش = بهای تمام‌شدهٔ فروش (COGS) ÷ ارزشِ موجودی،
/// و روزِ ماندگاری = روزهای دوره × ارزشِ موجودی ÷ COGS. منطقِ خالص و تست‌پذیر.
/// </summary>
public static class InventoryTurnover
{
    /// <summary>روزِ ماندگاری = -۱ یعنی «بی‌گردش» (موجودی دارد ولی در بازه فروشی نداشته).</summary>
    public static TurnoverResult Compute(decimal cogs, decimal inventoryValue, int periodDays)
    {
        if (inventoryValue <= 0) return new TurnoverResult(0m, 0m);
        var ratio = Math.Round(cogs / inventoryValue, 2);
        var days = cogs > 0 ? Math.Round(periodDays * inventoryValue / cogs, 1) : -1m;
        return new TurnoverResult(ratio, days);
    }
}
