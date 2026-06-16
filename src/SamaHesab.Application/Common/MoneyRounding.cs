namespace SamaHesab.Application.Common;

/// <summary>
/// 🇮🇷 POS-IR-1 — گرد کردنِ مبلغ به نزدیک‌ترین پله (رایج در فروشگاه‌های ایرانی؛ مثلاً ۱۰۰۰ یا ۵۰۰۰ ریال).
/// step ≤ 0 یعنی بدونِ گرد کردن (مبلغ بی‌تغییر).
/// </summary>
public static class MoneyRounding
{
    /// <summary>مبلغ را به نزدیک‌ترین مضربِ <paramref name="step"/> گرد می‌کند.</summary>
    public static decimal RoundTo(decimal amount, int step)
    {
        if (step <= 0) return amount;
        return System.Math.Round(amount / step, System.MidpointRounding.AwayFromZero) * step;
    }

    /// <summary>اختلافِ گرد کردن (مقدارِ گردشده − اصل). مثبت = به نفعِ فروشنده.</summary>
    public static decimal Adjustment(decimal amount, int step) => RoundTo(amount, step) - amount;
}
