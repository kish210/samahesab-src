using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SamaHesab.Application.Licensing;

/// <summary>
/// U-SUPPORT-RESET — پیش‌تر اگر کاربر هم رمز و هم کدِ بازیابیِ محلی را گم می‌کرد، هیچ راهی برایِ
/// پشتیبانی وجود نداشت که از راه دور کمک کند (این برنامه آفلاین است، بدونِ ایمیل/سرورِ مرکزی).
/// یک «کلیدِ اصلیِ» ثابت/backdoor برایِ دورزدنِ رمزِ همهٔ مشتری‌ها خودش یک حفرهٔ امنیتیِ جدی
/// است؛ به‌جایش همان الگویِ <see cref="RsaLicense"/> تکرار می‌شود: توکنِ کوتاه‌مدت که فقط برایِ
/// یک <b>Fingerprintِ مشخصِ ماشین</b> با کلیدِ خصوصیِ وندور امضا شده — روی هر دستگاهِ دیگری
/// یا بعد از انقضا بی‌اثر است.
/// </summary>
public sealed record SupportResetToken(string MachineFingerprint, DateTime IssuedAtUtc, DateTime ExpiresUtc)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public string Canonical =>
        string.Join("|", "srt-v1", MachineFingerprint.Trim().ToUpperInvariant(),
            IssuedAtUtc.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ"),
            ExpiresUtc.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ"));

    public byte[] CanonicalBytes() => Encoding.UTF8.GetBytes(Canonical);
}

/// <summary>توکن + امضا، به‌صورتِ یک رشتهٔ قابلِ کپی/پیست (نه فایل — پشتیبانی آن را از طریقِ
/// ایمیل/پیامک/چت برایِ مشتری می‌فرستد، نه دستهٔ کاملِ فایلِ لایسنس).</summary>
public sealed record SupportResetTokenDocument(SupportResetToken Token, string Signature)
{
    public string ToCode() =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(this)));

    public static SupportResetTokenDocument? FromCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(code.Trim()));
            return JsonSerializer.Deserialize<SupportResetTokenDocument>(json);
        }
        catch { return null; }
    }
}

/// <summary>امضا (فقط سمتِ ابزارِ پشتیبانی، با کلیدِ خصوصی) / تأیید (سمتِ کلاینت، با کلیدِ عمومی).</summary>
public static class SupportResetTokenSigner
{
    public static string Sign(SupportResetToken token, string privateKeyPem)
    {
        using var rsa = RSA.Create();
        rsa.ImportFromPem(privateKeyPem);
        var sig = rsa.SignData(token.CanonicalBytes(), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return Convert.ToBase64String(sig);
    }

    /// <summary>معتبر است اگر: امضا با کلیدِ عمومی تطبیق کند + Fingerprint دقیقاً همینِ ماشین باشد
    /// + هنوز منقضی نشده باشد (با ۵ دقیقه تحملِ اختلافِ ساعت).</summary>
    public static bool Verify(SupportResetTokenDocument? doc, string machineFingerprint, DateTime nowUtc, string publicKeyPem)
    {
        if (doc?.Signature is null || string.IsNullOrWhiteSpace(doc.Signature)) return false;
        if (!string.Equals(doc.Token.MachineFingerprint.Trim(), machineFingerprint.Trim(), StringComparison.OrdinalIgnoreCase))
            return false;
        if (nowUtc < doc.Token.IssuedAtUtc.AddMinutes(-5) || nowUtc > doc.Token.ExpiresUtc)
            return false;
        try
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(publicKeyPem);
            var sig = Convert.FromBase64String(doc.Signature);
            return rsa.VerifyData(doc.Token.CanonicalBytes(), sig, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        catch { return false; }
    }
}
