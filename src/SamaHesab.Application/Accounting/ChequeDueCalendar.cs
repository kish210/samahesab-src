using System.Globalization;

namespace SamaHesab.Application.Accounting;

/// <summary>یک چکِ ورودی برای تقویمِ سررسید (تاریخِ شمسیِ yyyy/MM/dd + مبلغ + دریافتی/پرداختی).</summary>
public record ChequeDueInput(string DueDate, decimal Amount, bool IsReceived);

/// <summary>سطلِ زمانیِ سررسید (سررسیدگذشته/امروز/هفتهٔ پیش‌رو/۳۰ روز/بعدتر) با جمعِ دریافتی و پرداختی.</summary>
public record ChequeDueBucket(string Key, string Label,
    int ReceivedCount, decimal ReceivedAmount, int PaidCount, decimal PaidAmount)
{
    public int TotalCount => ReceivedCount + PaidCount;
    /// <summary>خالصِ نقدینگیِ سطل = دریافتی − پرداختی.</summary>
    public decimal Net => ReceivedAmount - PaidAmount;
}

/// <summary>یک روزِ سررسید با جمعِ چک‌های همان روز.</summary>
public record ChequeDueDay(string DueDate, ChequeDueState State,
    int ReceivedCount, decimal ReceivedAmount, int PaidCount, decimal PaidAmount);

public record ChequeDueCalendarResult(
    IReadOnlyList<ChequeDueBucket> Buckets,
    IReadOnlyList<ChequeDueDay> Days,
    decimal TotalReceived, decimal TotalPaid)
{
    public decimal Net => TotalReceived - TotalPaid;
}

/// <summary>
/// تقویمِ سررسیدِ چک — منطقِ خالص و تست‌پذیر. چک‌های در جریان را بر اساسِ فاصلهٔ روزِ سررسید تا امروز
/// به سطل‌های زمانی و سپس به روزهای مجزا جمع می‌بندد. تاریخ‌ها شمسیِ yyyy/MM/dd هستند.
/// </summary>
public static class ChequeDueCalendar
{
    // کلید/برچسبِ سطل‌ها — مرتبه‌ی نمایش به همین ترتیب است.
    private static readonly (string Key, string Label, int MaxDays)[] BucketDefs =
    {
        ("overdue",  "سررسیدگذشته",        -1),  // قبل از امروز
        ("today",    "امروز",               0),
        ("week",     "۷ روزِ آینده",        7),
        ("month",    "۸ تا ۳۰ روزِ آینده", 30),
        ("later",    "بعد از ۳۰ روز",       int.MaxValue),
    };

    /// <summary>تبدیلِ تاریخِ شمسیِ yyyy/MM/dd به DateTime؛ نامعتبر → null.</summary>
    public static DateTime? ParseJalali(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var parts = s.Split('/');
        if (parts.Length != 3) return null;
        if (!int.TryParse(parts[0], out var y) || !int.TryParse(parts[1], out var m) || !int.TryParse(parts[2], out var d))
            return null;
        if (m < 1 || m > 12 || d < 1 || d > 31) return null;
        try { return new PersianCalendar().ToDateTime(y, m, d, 0, 0, 0, 0); }
        catch { return null; }
    }

    private static string BucketKeyForOffset(int dayOffset)
    {
        if (dayOffset < 0) return "overdue";
        if (dayOffset == 0) return "today";
        if (dayOffset <= 7) return "week";
        if (dayOffset <= 30) return "month";
        return "later";
    }

    public static ChequeDueCalendarResult Build(IEnumerable<ChequeDueInput> cheques, string today)
    {
        var todayDate = ParseJalali(today);

        // انباشتِ سطل‌ها (تعداد/مبلغِ دریافتی و پرداختی به‌تفکیکِ سطل)
        var rc = BucketDefs.ToDictionary(b => b.Key, _ => 0);
        var ra = BucketDefs.ToDictionary(b => b.Key, _ => 0m);
        var pc = BucketDefs.ToDictionary(b => b.Key, _ => 0);
        var pa = BucketDefs.ToDictionary(b => b.Key, _ => 0m);

        // انباشتِ روزها
        var dayAgg = new Dictionary<string, (int rc, decimal ra, int pc, decimal pa)>();

        decimal totalReceived = 0, totalPaid = 0;

        foreach (var c in cheques)
        {
            // سطل: اگر امروز یا تاریخِ چک نامعتبر باشد، در «بعدتر» قرار می‌گیرد (محافظه‌کارانه).
            string key = "later";
            var due = ParseJalali(c.DueDate);
            if (todayDate.HasValue && due.HasValue)
                key = BucketKeyForOffset((int)(due.Value.Date - todayDate.Value.Date).TotalDays);

            if (c.IsReceived) { rc[key]++; ra[key] += c.Amount; totalReceived += c.Amount; }
            else { pc[key]++; pa[key] += c.Amount; totalPaid += c.Amount; }

            var dkey = c.DueDate ?? string.Empty;
            var prev = dayAgg.TryGetValue(dkey, out var v) ? v : (0, 0m, 0, 0m);
            dayAgg[dkey] = c.IsReceived
                ? (prev.Item1 + 1, prev.Item2 + c.Amount, prev.Item3, prev.Item4)
                : (prev.Item1, prev.Item2, prev.Item3 + 1, prev.Item4 + c.Amount);
        }

        var buckets = BucketDefs
            .Select(b => new ChequeDueBucket(b.Key, b.Label, rc[b.Key], ra[b.Key], pc[b.Key], pa[b.Key]))
            .ToList();

        var days = dayAgg
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => new ChequeDueDay(
                kv.Key,
                todayDate.HasValue ? ChequeBoard.Classify(kv.Key, today) : ChequeDueState.Upcoming,
                kv.Value.rc, kv.Value.ra, kv.Value.pc, kv.Value.pa))
            .ToList();

        return new ChequeDueCalendarResult(buckets, days, totalReceived, totalPaid);
    }
}
