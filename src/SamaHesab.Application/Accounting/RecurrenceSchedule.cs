namespace SamaHesab.Application.Accounting;

public enum RecurrenceFrequency { Monthly = 0, Yearly = 1, Quarterly = 2, SemiAnnual = 3 }

/// <summary>
/// زمان‌بندی اسناد تکرارشونده — منطق خالص و تست‌پذیر روی تاریخ شمسی «YYYY/MM/DD».
/// دوره‌های ماه‌محور: ماهانه/فصلی/شش‌ماهه/سالانه (همه با محاسبه‌ی رشته‌ای بدون تبدیل تقویم).
/// روزِ بزرگ‌تر از ۲۹ به ۲۹ محدود می‌شود تا از تاریخ نامعتبر (مثل ۳۱ اسفند) جلوگیری شود.
/// </summary>
public static class RecurrenceSchedule
{
    /// <summary>آیا این تاریخِ سررسید نسبت به «امروز» رسیده است؟ (هر دو «YYYY/MM/DD»)</summary>
    public static bool IsDue(string nextDate, string today)
        => string.CompareOrdinal(nextDate, today) <= 0;

    /// <summary>تاریخ سررسیدِ بعدی پس از یک دوره.</summary>
    public static string NextAfter(string date, RecurrenceFrequency frequency)
    {
        var parts = date.Split('/');
        if (parts.Length != 3
            || !int.TryParse(parts[0], out var y)
            || !int.TryParse(parts[1], out var m)
            || !int.TryParse(parts[2], out var d))
            throw new ArgumentException($"تاریخ نامعتبر: {date}");

        // همهٔ دوره‌ها ماه‌محورند (بدونِ نیاز به تبدیلِ تقویم): ماهانه=۱ · فصلی=۳ · شش‌ماهه=۶ · سالانه=۱۲ ماه.
        var addMonths = frequency switch
        {
            RecurrenceFrequency.Monthly    => 1,
            RecurrenceFrequency.Quarterly  => 3,
            RecurrenceFrequency.SemiAnnual => 6,
            RecurrenceFrequency.Yearly     => 12,
            _                               => 1,
        };
        m += addMonths;
        while (m > 12) { m -= 12; y++; }
        if (d > 29) d = 29;
        return $"{y:0000}/{m:00}/{d:00}";
    }
}
