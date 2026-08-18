using SamaHesab.Domain.Enums;

namespace SamaHesab.Domain.Entities.Accounting;

/// <summary>
/// موتورِ خالصِ محاسبهٔ استهلاک (بدونِ وابستگی به DB/تقویم) — قابلِ تستِ واحد.
/// تاریخ‌ها رشتهٔ شمسیِ «yyyy/MM/dd» یا «yyyy/MM» هستند.
/// </summary>
public static class DepreciationCalculator
{
    /// <summary>استهلاکِ ماهانهٔ خط مستقیم: (بهای تمام‌شده − اسقاط) ÷ عمرِ مفیدِ ماهانه.</summary>
    public static decimal MonthlyStraightLine(decimal cost, decimal salvage, int lifeMonths)
    {
        if (lifeMonths <= 0) return 0;
        var depreciable = cost - salvage;
        return depreciable <= 0 ? 0 : depreciable / lifeMonths;
    }

    /// <summary>استهلاکِ ماهانهٔ نزولی (ماندهٔ کاهنده) رویِ ارزشِ دفتریِ جاری؛ هرگز زیرِ اسقاط نمی‌رود.</summary>
    public static decimal MonthlyDecliningBalance(decimal cost, decimal salvage, int lifeMonths, decimal bookValue)
    {
        if (lifeMonths <= 0 || cost <= 0 || bookValue <= salvage + 0.01m) return 0;

        // نرخ = 1 − (اسقاط/بها)^(1/عمر). اگر اسقاط صفر باشد، از نرخِ معادلِ خط مستقیم (2/عمر) استفاده می‌شود.
        double rate;
        if (salvage <= 0)
            rate = 2.0 / lifeMonths;
        else
            rate = 1 - Math.Pow((double)(salvage / cost), 1.0 / lifeMonths);

        var dep = bookValue * (decimal)rate;
        var floor = bookValue - salvage;   // کف: ارزشِ دفتری نباید از اسقاط پایین‌تر برود
        if (dep > floor) dep = floor;
        return dep < 0 ? 0 : dep;
    }

    /// <summary>ماهِ کل از تاریخِ «yyyy/MM/dd» یا «yyyy/MM».</summary>
    public static int TotalMonths(string jalaliDate)
    {
        var parts = (jalaliDate ?? string.Empty).Split('/');
        if (parts.Length < 2 || !int.TryParse(parts[0], out var y) || !int.TryParse(parts[1], out var m))
            return 0;
        return y * 12 + (m - 1);
    }

    /// <summary>تعدادِ ماه‌های بینِ دو تاریخ (مثبت یعنی تاریخِ دوم بعد از اولی است).</summary>
    public static int MonthsBetween(string fromDate, string toDate) =>
        TotalMonths(toDate) - TotalMonths(fromDate);

    /// <summary>
    /// استهلاکِ قابلِ اعمال برایِ `monthsToRun` ماه رویِ دارایی با `currentAccum` انباشته —
    /// خط مستقیم به‌صورتِ ضربِ ثابت، نزولی به‌صورتِ تکرارِ ماه‌به‌ماه رویِ ارزشِ دفتریِ در حالِ کاهش.
    /// خروجی هرگز از مبلغِ قابلِ‌استهلاکِ باقی‌مانده بیشتر نیست.
    /// </summary>
    public static decimal DepreciationForMonths(decimal cost, decimal salvage, int lifeMonths,
        DepreciationMethod method, decimal currentAccum, int monthsToRun)
    {
        if (monthsToRun <= 0) return 0;

        decimal total;
        if (method == DepreciationMethod.StraightLine)
        {
            total = MonthlyStraightLine(cost, salvage, lifeMonths) * monthsToRun;
        }
        else
        {
            var book = cost - currentAccum;
            total = 0;
            for (var i = 0; i < monthsToRun; i++)
            {
                var d = MonthlyDecliningBalance(cost, salvage, lifeMonths, book);
                if (d <= 0) break;
                total += d;
                book -= d;
            }
        }

        var remaining = Math.Max(0, (cost - currentAccum) - salvage);
        return total > remaining ? remaining : total;
    }
}
