using System.Security.Cryptography;
using System.Text;

namespace SamaHesab.Application.Licensing;

/// <summary>
/// فاز ۱۲ P-G7 — هستهٔ لایسنسِ آفلاینِ مقیّد به ماشین.
/// کلیدِ فعال‌سازی = HMAC-SHA256(شناسهٔ ماشین، رمزِ وندور) → ۲۰ نویسهٔ Base32، گروه‌بندی‌شده.
/// وندور با `Generate` کلید می‌سازد؛ برنامه با `Validate` (آفلاین، بدونِ سرور) بررسی می‌کند.
/// رمزِ وندور باید در نسخهٔ تولید جایگزین شود (از پیکربندی/سکرت).
/// </summary>
public static class LicenseKey
{
    private const string Base32 = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; // بدونِ I,O,0,1 برای خوانایی

    /// <summary>کلیدِ فعال‌سازی برای یک شناسهٔ ماشینِ مشخص می‌سازد (سمتِ وندور).</summary>
    public static string Generate(string machineId, string secret)
    {
        using var h = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = h.ComputeHash(Encoding.UTF8.GetBytes(Canon(machineId)));
        var sb = new StringBuilder(24);
        for (int i = 0; i < 20; i++)
        {
            if (i > 0 && i % 5 == 0) sb.Append('-');
            sb.Append(Base32[hash[i] & 31]);
        }
        return sb.ToString();   // XXXXX-XXXXX-XXXXX-XXXXX
    }

    /// <summary>کلیدِ واردشده را برای این ماشین اعتبارسنجی می‌کند (آفلاین).</summary>
    public static bool Validate(string machineId, string key, string secret)
    {
        if (string.IsNullOrWhiteSpace(key)) return false;
        var expected = StripSep(Generate(machineId, secret));
        var actual = StripSep(key);
        // مقایسهٔ ثابت‌زمان برای جلوگیری از نشتِ زمانی.
        if (expected.Length != actual.Length) return false;
        int diff = 0;
        for (int i = 0; i < expected.Length; i++) diff |= expected[i] ^ actual[i];
        return diff == 0;
    }

    private static string Canon(string s) => (s ?? "").Trim().ToUpperInvariant();
    private static string StripSep(string s) => Canon(s).Replace("-", "").Replace(" ", "");
}

/// <summary>سیاستِ تریال (پیش‌فرض ۳۰ روز — قابلِ‌تنظیم پس از تصمیمِ تجاری).</summary>
public static class TrialPolicy
{
    public const int DefaultTrialDays = 30;

    public static int DaysRemaining(DateTime installUtc, DateTime nowUtc, int trialDays = DefaultTrialDays)
    {
        var used = (int)System.Math.Floor((nowUtc.Date - installUtc.Date).TotalDays);
        return System.Math.Max(0, trialDays - used);
    }

    public static bool Expired(DateTime installUtc, DateTime nowUtc, int trialDays = DefaultTrialDays)
        => DaysRemaining(installUtc, nowUtc, trialDays) <= 0;
}
