using System;
using System.IO;
using SamaHesab.Application.Licensing;

namespace SamaHesab.WPF.Services;

public enum AppLicenseState { Trial, TrialExpired, Activated, Expired, Invalid }

/// <summary>وضعیتِ کلیِ لایسنسِ برنامه (برای گیت/بنر/پنجرهٔ فعال‌سازی).</summary>
public sealed record AppLicenseStatus(
    AppLicenseState State, string Message,
    int? TrialDaysRemaining, int? TrialVouchersRemaining, LicenseInfo? License)
{
    public bool CanRun => State is AppLicenseState.Trial or AppLicenseState.Activated;
}

/// <summary>
/// فاز ۱۲ P-G7 (رانتایم) — اعتبارسنجیِ آفلاینِ لایسنس در کلاینت:
/// فایلِ `license.lic` (امضاشده با RSA) → معتبر/منقضی؛ وگرنه نسخهٔ آزمایشی (۱۲۰ روز یا ۲۰۰ سند).
/// کلیدِ خصوصی هرگز اینجا نیست؛ فقط `LicensePublicKey.Pem`.
/// </summary>
public sealed class LicenseService
{
    private readonly IMachineFingerprintProvider _fp;
    private readonly LicenseValidator _validator;

    public LicenseService(IMachineFingerprintProvider fp)
    {
        _fp = fp;
        _validator = new LicenseValidator(LicensePublicKey.Pem);
    }

    public string MachineFingerprint => _fp.GetFingerprint();
    public string LicenseFilePath => Path.Combine(AppSettingsStore.AppDataDir, "license.lic");

    /// <summary>وضعیتِ فعلی. <paramref name="voucherCount"/> برای سقفِ ۲۰۰ سندِ تریال.</summary>
    public AppLicenseStatus GetStatus(int voucherCount = 0)
    {
        // ۱) لایسنسِ نصب‌شده؟
        if (File.Exists(LicenseFilePath))
        {
            LicenseDocument? doc = null;
            try { doc = LicenseDocument.FromJson(File.ReadAllText(LicenseFilePath)); } catch { }
            var c = _validator.Validate(doc, MachineFingerprint, DateTime.UtcNow);
            return c.Status switch
            {
                LicenseStatus.Valid => new(AppLicenseState.Activated, c.Message, null, null, c.License),
                LicenseStatus.Expired => new(AppLicenseState.Expired, c.Message, null, null, c.License),
                _ => new(AppLicenseState.Invalid, c.Message, null, null, c.License),
            };
        }

        // ۱.۵) فعال‌سازیِ سروری (تأییدِ سایت از /register): اگر انقضای ذخیره‌شده در آینده باشد → فعال.
        var gs = AppSettingsStore.GetGeneral();
        if (!string.IsNullOrWhiteSpace(gs.ServerLicenseExpiryUtc) &&
            DateTime.TryParse(gs.ServerLicenseExpiryUtc, null,
                System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal,
                out var srvExp) && srvExp > DateTime.UtcNow)
        {
            var d = (int)Math.Ceiling((srvExp - DateTime.UtcNow).TotalDays);
            return new(AppLicenseState.Activated,
                $"فعال (تأییدِ سایت) — {d} روز باقی‌مانده · رده {gs.ServerLicenseTier}", null, null, null);
        }

        // ۲) نسخهٔ آزمایشی (۱۲۰ روز یا ۲۰۰ سند)
        var install = GetOrSetTrialInstall();
        var (state, days, vouchers) = TrialPolicy.Evaluate(install, DateTime.UtcNow, voucherCount);
        return state == TrialState.Active
            ? new(AppLicenseState.Trial, $"نسخهٔ آزمایشی — {days} روز و {vouchers} سندِ باقی‌مانده", days, vouchers, null)
            : new(AppLicenseState.TrialExpired, "دورهٔ آزمایشی به پایان رسیده است (۱۲۰ روز یا ۲۰۰ سند).", 0, vouchers, null);
    }

    /// <summary>نصبِ فایلِ لایسنس از مسیرِ داده‌شده (پس از اعتبارسنجی کپی می‌شود).</summary>
    public (bool ok, string message) InstallLicense(string sourcePath)
    {
        try
        {
            var doc = LicenseDocument.FromJson(File.ReadAllText(sourcePath));
            var c = _validator.Validate(doc, MachineFingerprint, DateTime.UtcNow);
            if (c.Status != LicenseStatus.Valid) return (false, c.Message);
            Directory.CreateDirectory(AppSettingsStore.AppDataDir);
            File.Copy(sourcePath, LicenseFilePath, overwrite: true);
            return (true, c.Message);
        }
        catch (Exception ex) { return (false, "خطا در خواندنِ فایلِ لایسنس: " + ex.Message); }
    }

    private static DateTime GetOrSetTrialInstall()
    {
        var g = AppSettingsStore.GetGeneral();
        if (DateTime.TryParse(g.TrialInstallUtc, null,
                System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal,
                out var dt))
            return dt;
        var now = DateTime.UtcNow;
        g.TrialInstallUtc = now.ToString("o");
        AppSettingsStore.SaveGeneral(g);
        return now;
    }
}
