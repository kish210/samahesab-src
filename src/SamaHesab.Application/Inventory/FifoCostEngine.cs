namespace SamaHesab.Application.Inventory;

/// <summary>
/// کار #۲۹ — موتور بهای تمام‌شده‌ی FIFO (منطق خالص و تست‌پذیر).
/// حرکت‌ها به‌ترتیب زمان داده می‌شوند: مقدار مثبت = ورود (با بهای واحد)، منفی = خروج
/// (بها از قدیمی‌ترین لایه برداشته می‌شود). خروجی: بهای کل کالای خارج‌شده + لایه‌ها و ارزشِ باقی‌مانده.
/// </summary>
public static class FifoCostEngine
{
    public record Layer(decimal Qty, decimal UnitCost);
    public record Valuation(decimal IssuedCost, decimal RemainingQty, decimal RemainingValue,
        IReadOnlyList<Layer> RemainingLayers);

    public static Valuation Compute(IEnumerable<(decimal Qty, decimal UnitCost)> movements)
    {
        var layers = new LinkedList<Layer>();
        decimal issuedCost = 0;
        decimal lastCost = 0;

        foreach (var (qty, unitCost) in movements)
        {
            if (qty > 0)
            {
                layers.AddLast(new Layer(qty, unitCost));
                lastCost = unitCost;
            }
            else if (qty < 0)
            {
                var need = -qty;
                while (need > 0 && layers.First is not null)
                {
                    var f = layers.First.Value;
                    var take = Math.Min(need, f.Qty);
                    issuedCost += take * f.UnitCost;
                    need -= take;
                    if (take >= f.Qty) layers.RemoveFirst();
                    else layers.First.Value = f with { Qty = f.Qty - take };
                }
                // اگر موجودیِ لایه‌ای کافی نبود (خروج بیش از ورودِ ثبت‌شده)، با آخرین بها محاسبه کن
                if (need > 0) issuedCost += need * lastCost;
            }
        }

        var remainingQty = layers.Sum(l => l.Qty);
        var remainingValue = layers.Sum(l => l.Qty * l.UnitCost);
        return new Valuation(issuedCost, remainingQty, remainingValue, layers.ToList());
    }
}
