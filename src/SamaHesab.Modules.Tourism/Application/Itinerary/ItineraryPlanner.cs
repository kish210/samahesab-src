namespace SamaHesab.Modules.Tourism.Application.Itinerary;

/// <summary>یک گزینهٔ نامزدِ برنامه: محصول+سانس در یک روزِ مشخص (ورودیِ الگوریتم).</summary>
public sealed record PlanCandidate(
    int ProductId, string ProductName, int SessionId, int Day,
    int StartMinute, int EndMinute, decimal SalePrice, decimal Cost)
{
    public decimal Profit => SalePrice - Cost;
}

/// <summary>تنظیماتِ تولیدِ برنامه.</summary>
public sealed record PlanOptions(int Days, bool PreferVariety = true, int? MaxPerDay = null);

/// <summary>یک قلمِ برنامهٔ پیشنهادی.</summary>
public sealed record PlanStop(
    int Day, int ProductId, string ProductName, int SessionId,
    int StartMinute, int EndMinute, decimal SalePrice, decimal Cost, int SortOrder);

/// <summary>برنامهٔ پیشنهادیِ کامل + جمع‌ها.</summary>
public sealed record ItineraryPlan(IReadOnlyList<PlanStop> Stops)
{
    public decimal TotalSale => Stops.Sum(s => s.SalePrice);
    public decimal TotalCost => Stops.Sum(s => s.Cost);
    public decimal TotalProfit => Stops.Sum(s => s.SalePrice - s.Cost);
}

/// <summary>
/// الگوریتمِ هوشمندِ پیشنهادِ برنامهٔ اقامتی — منطقِ خالص و تست‌پذیر (بدونِ I/O، بدونِ DB).
/// قواعد (به‌ترتیبِ اولویت): (۱) **سودِ بالاتر**؛ (۲) **عدمِ تداخلِ سانسِ زمانی** در یک روز؛
/// (۳) **تنوع** (پرهیز از تکرارِ یک محصول در کلِ برنامه تا جای ممکن).
/// روشِ حریصانهٔ گام‌به‌گام: در هر گام بهترین نامزدِ مجازِ روز انتخاب می‌شود — اول محصولِ تکراری‌نشده،
/// سپس بیشترین سود، سپس زودترین سانس. این روش، تداخل را حذف و تنوع را بر سود مقدم می‌کند (بدونِ بک‌ترکِ پرهزینه).
/// </summary>
public static class ItineraryPlanner
{
    public static ItineraryPlan Plan(IEnumerable<PlanCandidate> candidates, PlanOptions options)
    {
        if (options.Days <= 0) return new ItineraryPlan(System.Array.Empty<PlanStop>());

        var all = candidates.ToList();
        var chosen = new List<PlanStop>();
        var usedProducts = new HashSet<int>();

        for (int day = 1; day <= options.Days; day++)
        {
            var remaining = all.Where(c => c.Day == day).ToList();
            var dayChosen = new List<(int Start, int End)>();
            var dayProducts = new HashSet<int>();   // یک محصول در یک روز فقط یک‌بار
            int order = 0;

            while (options.MaxPerDay is not int max || dayChosen.Count < max)
            {
                // نامزدهای مجاز: بدونِ تداخلِ زمانی و بدونِ تکرارِ محصول در همان روز.
                var feasible = remaining.Where(c =>
                    !dayProducts.Contains(c.ProductId) &&
                    !dayChosen.Any(s => c.StartMinute < s.End && s.Start < c.EndMinute)).ToList();
                if (feasible.Count == 0) break;

                var pick = feasible
                    .OrderByDescending(c => options.PreferVariety && !usedProducts.Contains(c.ProductId) ? 1 : 0)
                    .ThenByDescending(c => c.Profit)
                    .ThenBy(c => c.StartMinute)
                    .First();

                chosen.Add(new PlanStop(day, pick.ProductId, pick.ProductName, pick.SessionId,
                    pick.StartMinute, pick.EndMinute, pick.SalePrice, pick.Cost, order++));
                dayChosen.Add((pick.StartMinute, pick.EndMinute));
                dayProducts.Add(pick.ProductId);
                usedProducts.Add(pick.ProductId);
                remaining.Remove(pick);
            }
        }

        return new ItineraryPlan(chosen);
    }
}
