namespace SamaHesab.Application.Reports;

/// <summary>ورودیِ تحلیلِ ABC: شناسه + ارزش (مثلاً ارزشِ فروشِ کالا).</summary>
public record AbcInput(int Id, decimal Value);

/// <summary>یک قلمِ طبقه‌بندی‌شده: سهم و درصدِ تجمعی + طبقهٔ A/B/C.</summary>
public record AbcClassified(int Id, decimal Value, decimal SharePercent, decimal CumulativePercent, char Class);

/// <summary>
/// فاز ۱۲ (پولیش) — تحلیلِ ABC (پارِتو): اقلام را بر اساسِ ارزش نزولی مرتب و بر پایهٔ درصدِ تجمعی
/// طبقه‌بندی می‌کند: A تا <paramref name="aCut"/>٪ · B تا <paramref name="bCut"/>٪ · C باقی.
/// منطقِ خالص و تست‌پذیر (بدونِ UI/EF).
/// </summary>
public static class AbcEngine
{
    public static List<AbcClassified> Classify(IEnumerable<AbcInput> items, decimal aCut = 80m, decimal bCut = 95m)
    {
        var list = items.Where(i => i.Value > 0).OrderByDescending(i => i.Value).ToList();
        var total = list.Sum(i => i.Value);
        var result = new List<AbcClassified>(list.Count);
        decimal cum = 0m;
        foreach (var i in list)
        {
            var share = total > 0 ? i.Value / total * 100m : 0m;
            cum += share;
            var cls = cum <= aCut ? 'A' : cum <= bCut ? 'B' : 'C';
            result.Add(new AbcClassified(i.Id, i.Value, Math.Round(share, 2), Math.Round(cum, 2), cls));
        }
        return result;
    }
}
