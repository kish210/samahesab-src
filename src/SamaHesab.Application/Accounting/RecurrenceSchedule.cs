namespace SamaHesab.Application.Accounting;

public enum RecurrenceFrequency { Monthly = 0, Yearly = 1 }

/// <summary>
/// زمان‌بندی اسناد تکرارشونده — منطق خالص و تست‌پذیر روی تاریخ شمسی «YYYY/MM/DD».
/// فقط دوره‌های ماهانه/سالانه (که با محاسبه‌ی رشته‌ای بدون تبدیل تقویم قابل‌انجام‌اند) پشتیبانی می‌شوند.
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

        switch (frequency)
        {
            case RecurrenceFrequency.Monthly:
                m++;
                if (m > 12) { m = 1; y++; }
                break;
            case RecurrenceFrequency.Yearly:
                y++;
                break;
        }
        if (d > 29) d = 29;
        return $"{y:0000}/{m:00}/{d:00}";
    }
}
