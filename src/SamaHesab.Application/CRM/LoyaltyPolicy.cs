namespace SamaHesab.Application.CRM;

/// <summary>
/// کار #۳۸ — سیاست امتیاز باشگاه مشتریان (منطق خالص و تست‌پذیر).
/// قاعده‌ی پیش‌فرض: به ازای هر `RialsPerPoint` ریالِ خرید، ۱ امتیاز (به‌پایین گرد می‌شود).
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
