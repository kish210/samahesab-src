namespace SamaHesab.WPF.Services;

/// <summary>
/// کپیِ فایلِ بکاپ در پوشهٔ ابری (Google Drive for Desktop). منبعِ واحد برای هر دو مسیرِ
/// بکاپِ دستی و خودکار تا رفتار یکسان باشد. مقصدِ سینک از تنظیماتِ محلی (CloudBackupFolder) خوانده می‌شود.
/// </summary>
public static class CloudBackup
{
    /// <summary>آیا پوشهٔ ابری تنظیم شده است؟</summary>
    public static bool IsConfigured =>
        !string.IsNullOrWhiteSpace(AppSettingsStore.GetGeneral().CloudBackupFolder);

    /// <summary>
    /// کپیِ فایلِ بکاپ در پوشهٔ ابری در صورتِ تنظیم. مسیرِ مقصد یا null (تنظیم‌نشده/فایلِ نامعتبر).
    /// در صورتِ خطا، استثنا پرتاب می‌شود (فراخوان باید بگیرد).
    /// </summary>
    public static string? CopyIfConfigured(string? backupFile)
    {
        if (string.IsNullOrWhiteSpace(backupFile) || !System.IO.File.Exists(backupFile)) return null;
        var folder = (AppSettingsStore.GetGeneral().CloudBackupFolder ?? "").Trim();
        if (string.IsNullOrWhiteSpace(folder)) return null;

        System.IO.Directory.CreateDirectory(folder);
        var dest = System.IO.Path.Combine(folder, System.IO.Path.GetFileName(backupFile));
        System.IO.File.Copy(backupFile, dest, overwrite: true);
        return dest;
    }
}
