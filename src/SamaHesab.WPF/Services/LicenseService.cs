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

    private const string RegistryKeyPath = @"Software\SamaHesab";
    private const string RegistryValueName = "TrialInstallUtc";

    /// <summary>
    /// امنیتِ لایسنس: تاریخِ شروعِ تریال هم در settings.json (AppData) و هم در رجیستریِ HKCU
    /// نگه‌داری می‌شود. حذفِ فایلِ تنظیمات به‌تنهایی دیگر تریال را ریست نمی‌کند — قدیمی‌ترینِ
    /// تاریخِ معتبرِ موجود بینِ این دو منبع مبنا قرار می‌گیرد و در هر دو هم‌گام (sync) نگه داشته می‌شود.
    /// </summary>
    private static DateTime GetOrSetTrialInstall()
    {
        var g = AppSettingsStore.GetGeneral();
        var fromFile = TryParseUtc(g.TrialInstallUtc);
        var fromRegistry = TryParseUtc(ReadRegistryValue());

        DateTime? earliest = fromFile.HasValue && fromRegistry.HasValue
            ? (fromFile.Value < fromRegistry.Value ? fromFile.Value : fromRegistry.Value)
            : fromFile ?? fromRegistry;

        if (earliest.HasValue)
        {
            var iso = earliest.Value.ToString("o");
            if (g.TrialInstallUtc != iso) { g.TrialInstallUtc = iso; AppSettingsStore.SaveGeneral(g); }
            WriteRegistryValue(iso);
            return earliest.Value;
        }

        var now = DateTime.UtcNow;
        var nowIso = now.ToString("o");
        g.TrialInstallUtc = nowIso;
        AppSettingsStore.SaveGeneral(g);
        WriteRegistryValue(nowIso);
        return now;
    }

    private static DateTime? TryParseUtc(string? s) =>
        DateTime.TryParse(s, null,
            System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal,
            out var dt) ? dt : null;

    private static string? ReadRegistryValue()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(RegistryKeyPath);
            return key?.GetValue(RegistryValueName) as string;
        }
        catch { return null; }
    }

    private static void WriteRegistryValue(string iso)
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(RegistryKeyPath);
            key.SetValue(RegistryValueName, iso);
        }
        catch { /* بدونِ دسترسیِ رجیستری، فایلِ تنظیمات به‌تنهایی مبنا می‌ماند */ }
    }
}
