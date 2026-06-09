namespace SamaHesab.Application.Accounting;

/// <summary>وضعیت سررسید چکِ در جریان نسبت به امروز.</summary>
public enum ChequeDueState
{
    Overdue,    // سررسید گذشته
    DueToday,   // سررسید امروز
    Upcoming    // پیش رو
}

/// <summary>
/// طبقه‌بندی چک بر اساس سررسید — منطق خالص و تست‌پذیر.
/// تاریخ‌ها شمسیِ yyyy/MM/dd هستند؛ مقایسه‌ی لغوی = مقایسه‌ی زمانی.
/// </summary>
public static class ChequeBoard
{
    public static ChequeDueState Classify(string dueDate, string today)
    {
        var cmp = string.CompareOrdinal(dueDate, today);
        if (cmp < 0) return ChequeDueState.Overdue;
        if (cmp == 0) return ChequeDueState.DueToday;
        return ChequeDueState.Upcoming;
    }
}
