namespace SamaHesab.Application.CRM;

/// <summary>
/// سیاستِ امتیازِ باشگاهِ مشتریان (منطقِ خالص و تست‌پذیر). ماژولِ CRM (فاز ۳ استخراج).
/// namespace به‌عمد `SamaHesab.Application.CRM` نگه داشته شد تا مصرف‌کننده‌ها (VM/کنترلر/تست) نشکنند.
/// قاعدهٔ پیش‌فرض: به‌ازای هر `RialsPerPoint` ریالِ خرید، ۱ امتیاز (به‌پایین گرد).
/// </summary>
public static class LoyaltyPolicy
{
    public const decimal DefaultRialsPerPoint = 100_000m;   // هر ۱۰۰٬۰۰۰ ریال = ۱ امتیاز

    public static int EarnedPoints(decimal amount, decimal rialsPerPoint = 0)
    {
        var rate = rialsPerPoint <= 0 ? DefaultRialsPerPoint : rialsPerPoint;
        if (amount <= 0) return 0;
        return (int)Math.Floor(amount / rate);
    }

    public static bool CanRedeem(int balance, int points) => points > 0 && points <= balance;
}
