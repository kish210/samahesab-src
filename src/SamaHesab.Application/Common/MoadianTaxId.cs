using System.Text;

namespace SamaHesab.Application.Common;

/// <summary>
/// 🇮🇷 POS-IR-4 — زیرساختِ آفلاینِ «شمارهٔ منحصربه‌فردِ مالیاتی» سامانهٔ مودیان.
/// قالبِ مستند: ۲۲ نویسهٔ Base36 (۰-۹ و A-Z) = [۶ شناسهٔ یکتای حافظهٔ مالیاتی] +
/// [۵ بخشِ تاریخ (روزشمار، Base36)] + [۱۱ سریالِ داخلی (Base36)]. روی رسید به‌صورتِ QR چاپ می‌شود.
/// <para>⚠️ <b>مبدأِ روزشمار و جزئیاتِ کدگذاری باید پیش از تولید با اسپکِ رسمیِ سازمانِ امور مالیاتی
/// تطبیق داده شود.</b> شناسهٔ حافظه را سازمان به دستگاه می‌دهد. ارسالِ آنلاین به API مودیان خارج از این بخش است.</para>
/// </summary>
public static class MoadianTaxId
{
    private const string Base36Chars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    /// <summary>مبدأِ روزشمارِ بخشِ تاریخ (قابلِ‌تنظیم — باید با اسپکِ رسمی یکی شود).</summary>
    public static readonly DateTime DateEpoch = new(2016, 3, 20, 0, 0, 0, DateTimeKind.Utc);

    /// <summary>شمارهٔ منحصربه‌فردِ ۲۲نویسه‌ای را می‌سازد.</summary>
    public static string Generate(string memoryId, DateTime issuedUtc, long serial)
    {
        var mem = Fit(Canon(memoryId), 6);
        var days = System.Math.Max(0, (int)(issuedUtc.Date - DateEpoch.Date).TotalDays);
        return mem + Base36(days, 5) + Base36(serial, 11);   // ۶+۵+۱۱ = ۲۲
    }

    /// <summary>محتوای QR مودیان = همان شمارهٔ یکتای مالیاتی.</summary>
    public static string QrPayload(string taxId) => taxId;

    // ── کمکی‌ها ──
    private static string Canon(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return "";
        var sb = new StringBuilder();
        foreach (var ch in s.Trim().ToUpperInvariant())
            if (Base36Chars.IndexOf(ch) >= 0) sb.Append(ch);
        return sb.ToString();
    }

    /// <summary>به طولِ <paramref name="width"/>: کوتاه → چپ‌پُر با '0'؛ بلند → <paramref name="width"/> نویسهٔ آخر.</summary>
    private static string Fit(string s, int width)
        => s.Length == width ? s : s.Length < width ? s.PadLeft(width, '0') : s[^width..];

    private static string Base36(long value, int width)
    {
        if (value < 0) value = 0;
        var sb = new StringBuilder();
        do { sb.Insert(0, Base36Chars[(int)(value % 36)]); value /= 36; } while (value > 0);
        return Fit(sb.ToString(), width);
    }
}
