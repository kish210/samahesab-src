namespace SamaHesab.Modules.Tourism.Application;

/// <summary>
/// منطقِ خالصِ نمای لیستِ فروشِ گردشگری — نزدیک‌ترین تاریخِ سفر و فیلترِ بازهٔ تاریخ.
/// جدا از کوئری تا تست‌پذیر بماند. تاریخ‌ها شمسیِ YYYY/MM/DD (مقایسهٔ رشته‌ای = زمانی).
/// </summary>
public static class TourismSalesView
{
    /// <summary>کوچک‌ترین تاریخِ سفرِ ناتهی (نزدیک‌ترین)؛ اگر همه تهی باشند → null.</summary>
    public static string? NearestTravelDate(IEnumerable<string?> travelDates)
    {
        string? best = null;
        foreach (var d in travelDates)
        {
            if (string.IsNullOrWhiteSpace(d)) continue;
            if (best is null || string.Compare(d, best, StringComparison.Ordinal) < 0) best = d;
        }
        return best;
    }

    /// <summary>آیا تاریخِ شمسی در بازهٔ [from, to] است؟ (مرزها شامل؛ مرزِ تهی = بی‌قید).</summary>
    public static bool InRange(string date, string? from, string? to) =>
        (string.IsNullOrWhiteSpace(from) || string.Compare(date, from, StringComparison.Ordinal) >= 0)
        && (string.IsNullOrWhiteSpace(to) || string.Compare(date, to, StringComparison.Ordinal) <= 0);
}
