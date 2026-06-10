namespace SamaHesab.Application.Automation;

/// <summary>ورودی یک کالا برای محاسبه‌ی پیشنهاد سفارش.</summary>
public record ReorderInput(int ProductId, string Name, decimal OnHand,
    decimal MinStock, decimal? ReorderPoint, decimal? MaxStock);

/// <summary>پیشنهاد سفارش خرید برای یک کالا.</summary>
public record ReorderSuggestion(int ProductId, string Name, decimal OnHand,
    decimal Threshold, decimal SuggestedQty);

/// <summary>
/// موتور پیشنهاد خودکار سفارش خرید — منطق خالص و تست‌پذیر.
/// آستانه = ReorderPoint (یا MinStock). وقتی موجودی ≤ آستانه، پیشنهاد تا سقف
/// (MaxStock یا دو برابر آستانه) داده می‌شود.
/// </summary>
public static class ReorderEngine
{
    public static List<ReorderSuggestion> Suggest(IEnumerable<ReorderInput> products)
    {
        var result = new List<ReorderSuggestion>();
        foreach (var p in products)
        {
            var threshold = (p.ReorderPoint is > 0) ? p.ReorderPoint!.Value : p.MinStock;
            if (threshold <= 0) continue;
            if (p.OnHand > threshold) continue;

            var target = (p.MaxStock is > 0 && p.MaxStock!.Value >= threshold)
                ? p.MaxStock!.Value
                : threshold * 2;
            var qty = target - p.OnHand;
            if (qty <= 0) continue;

            result.Add(new ReorderSuggestion(p.ProductId, p.Name, p.OnHand, threshold, qty));
        }
        return result
            .OrderByDescending(r => r.Threshold == 0 ? 0 : (r.Threshold - r.OnHand) / r.Threshold) // فوری‌ترین اول
            .ThenBy(r => r.ProductId)
            .ToList();
    }
}
