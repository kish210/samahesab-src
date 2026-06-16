namespace SamaHesab.Application.Licensing;

/// <summary>فاز ۱۲ P-G7 — ردهٔ لایسنس (سقفِ شعبه/کاربر؛ همه ۱ شرکت).</summary>
public enum LicenseTier
{
    Trial = 0,        // نسخهٔ آزمایشی
    Starter = 1,      // ۱ شعبه · تا ۳ کاربر
    Professional = 2, // تا ۳ شعبه · تا ۱۰ کاربر
    Enterprise = 3    // شعبه/کاربرِ نامحدود
}

/// <summary>سقف‌های هر رده (در لایسنسِ امضاشده صریح ذخیره می‌شوند؛ این مقادیر مرجعِ تولید/پیش‌فرض‌اند).</summary>
public static class LicenseLimits
{
    public const int Unlimited = int.MaxValue;

    public static (int MaxBranches, int MaxUsers) For(LicenseTier tier) => tier switch
    {
        LicenseTier.Starter      => (1, 3),
        LicenseTier.Professional => (3, 10),
        LicenseTier.Enterprise   => (Unlimited, Unlimited),
        _                        => (1, 1),   // Trial: ۱ شعبه/۱ کاربرِ مؤثر
    };

    public static bool IsUnlimited(int value) => value >= Unlimited;
}
