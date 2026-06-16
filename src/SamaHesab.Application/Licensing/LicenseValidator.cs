namespace SamaHesab.Application.Licensing;

public enum LicenseStatus
{
    None,          // لایسنسی نصب نشده
    Valid,         // معتبر و فعال
    Expired,       // منقضی
    WrongMachine,  // برای دستگاهِ دیگری صادر شده
    BadSignature   // امضا نامعتبر/دستکاری‌شده
}

/// <summary>نتیجهٔ اعتبارسنجیِ لایسنس (graceful — همیشه پیامِ فارسی دارد).</summary>
public sealed record LicenseCheck(LicenseStatus Status, LicenseInfo? License, string Message)
{
    public bool IsValid => Status == LicenseStatus.Valid;
}

/// <summary>
/// فاز ۱۲ P-G7 — اعتبارسنجیِ آفلاینِ لایسنس: تأییدِ امضای RSA (با کلیدِ عمومی) +
/// تطبیقِ اثرِانگشتِ دستگاه + بررسیِ انقضا. تشخیصِ دستکاری از طریقِ شکستِ امضا.
/// </summary>
public sealed class LicenseValidator
{
    private readonly string _publicKeyPem;
    public LicenseValidator(string publicKeyPem) => _publicKeyPem = publicKeyPem;

    public LicenseCheck Validate(LicenseDocument? doc, string currentFingerprint, DateTime nowUtc)
    {
        if (doc is null)
            return new(LicenseStatus.None, null, "لایسنسی نصب نشده است.");

        if (!RsaLicense.Verify(doc, _publicKeyPem))
            return new(LicenseStatus.BadSignature, null, "امضای لایسنس نامعتبر است (احتمالِ دستکاری).");

        var lic = doc.License;

        if (!string.Equals(lic.MachineFingerprint?.Trim(), currentFingerprint?.Trim(),
                System.StringComparison.OrdinalIgnoreCase))
            return new(LicenseStatus.WrongMachine, lic, "این لایسنس برای دستگاهِ فعلی صادر نشده است.");

        if (nowUtc.ToUniversalTime() >= lic.ExpiresUtc.ToUniversalTime())
            return new(LicenseStatus.Expired, lic, $"لایسنس در {lic.ExpiresUtc:yyyy/MM/dd} منقضی شده است.");

        return new(LicenseStatus.Valid, lic, $"لایسنسِ معتبر — رده: {lic.Tier} · انقضا: {lic.ExpiresUtc:yyyy/MM/dd}");
    }
}
