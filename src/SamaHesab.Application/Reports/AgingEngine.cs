namespace SamaHesab.Application.Reports;

/// <summary>
/// فاز ۱۲ (RC) — موتورِ خالصِ ماندهٔ سنی‌شده (Aged balance). مبلغِ بازِ هر سند را بر اساسِ
/// تعدادِ روزِ گذشته از سررسید در یکی از سطل‌ها قرار می‌دهد. بدونِ وابستگی به UI/EF/تقویم
/// (تبدیلِ تاریخِ شمسی→میلادی و محاسبهٔ روز در لایهٔ کوئری انجام می‌شود).
/// مرزها: جاری/سررسیدنشده ۰–۳۰ · ۳۱–۶۰ · ۶۱–۹۰ · بیش از ۹۰.
/// </summary>
public static class AgingEngine
{
    /// <summary>شمارهٔ سطل بر اساسِ سنِ روز: 0=۰–۳۰، 1=۳۱–۶۰، 2=۶۱–۹۰، 3=بیش از ۹۰.</summary>
    public static int BucketOf(int ageDays)
        => ageDays <= 30 ? 0 : ageDays <= 60 ? 1 : ageDays <= 90 ? 2 : 3;
}

/// <summary>انباشتگرِ سطل‌های سنی برای یک طرف‌حساب.</summary>
public sealed class AgingBuckets
{
    public decimal Current { get; private set; }   // ۰–۳۰ (شاملِ سررسیدنشده)
    public decimal D31_60 { get; private set; }
    public decimal D61_90 { get; private set; }
    public decimal Over90 { get; private set; }
    public decimal Total => Current + D31_60 + D61_90 + Over90;

    public void Add(decimal amount, int ageDays)
    {
        switch (AgingEngine.BucketOf(ageDays))
        {
            case 0: Current += amount; break;
            case 1: D31_60 += amount; break;
            case 2: D61_90 += amount; break;
            default: Over90 += amount; break;
        }
    }
}
