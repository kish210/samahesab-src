using SamaHesab.Application.Licensing;

// U-SUPPORT-RESET — ابزارِ آفلاینِ صدورِ کدِ ریست برایِ مشتری‌ای که هم رمز و هم کدِ بازیابیِ
// محلی‌اش را گم کرده. فقط دستِ پشتیبانی/وندور اجرا می‌شود؛ کلیدِ خصوصیِ RSA همان کلیدی است که
// LicensePublicKey.Pem (در src/SamaHesab.Application/Licensing/) با کلیدِ عمومیِ آن ساخته شده —
// این کلیدِ خصوصی هرگز در این مخزن نیست، پشتیبانی آن را به‌صورتِ فایلِ PEM محلی نگه می‌دارد.
//
// استفاده:
//   samahesab-support-tool reset-token --fingerprint <کدِ دستگاهی که مشتری خواند> --key <مسیرِ فایلِ کلیدِ خصوصیِ PEM> [--days 2]
//
// خروجی: یک رشتهٔ base64 که همان‌طور که هست (کپی/پیست، نه تایپِ دستی) برایِ مشتری فرستاده می‌شود
// تا در پنجرهٔ «فراموشیِ رمز → کمکِ پشتیبانی» بچسباند.

if (args.Length == 0 || args[0] != "reset-token")
{
    Console.WriteLine("استفاده: samahesab-support-tool reset-token --fingerprint <کدِ دستگاه> --key <مسیرِ کلیدِ خصوصیِ PEM> [--days 2]");
    return 1;
}

string? fingerprint = null, keyPath = null;
var days = 2;
for (var i = 1; i < args.Length - 1; i++)
{
    switch (args[i])
    {
        case "--fingerprint": fingerprint = args[++i]; break;
        case "--key": keyPath = args[++i]; break;
        case "--days": days = int.Parse(args[++i]); break;
    }
}

if (string.IsNullOrWhiteSpace(fingerprint) || string.IsNullOrWhiteSpace(keyPath))
{
    Console.WriteLine("خطا: --fingerprint و --key الزامی‌اند.");
    return 1;
}
if (!File.Exists(keyPath))
{
    Console.WriteLine($"خطا: فایلِ کلید یافت نشد: {keyPath}");
    return 1;
}

var privateKeyPem = File.ReadAllText(keyPath);
var now = DateTime.UtcNow;
var token = new SupportResetToken(fingerprint, now, now.AddDays(days));
var signature = SupportResetTokenSigner.Sign(token, privateKeyPem);
var doc = new SupportResetTokenDocument(token, signature);

Console.WriteLine();
Console.WriteLine($"کدِ ریست (معتبر تا {token.ExpiresUtc:yyyy-MM-dd HH:mm} UTC، فقط برایِ همین دستگاه):");
Console.WriteLine();
Console.WriteLine(doc.ToCode());
Console.WriteLine();
return 0;
