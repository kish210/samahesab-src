using System.IO;

namespace SamaHesab.WPF.Services;

/// <summary>
/// میانبرِ دسکتاپ per ماژول — نصاب فقط میانبرِ برنامهٔ اصلی را می‌سازد؛ میانبرِ هر ماژول
/// هنگامِ فعال‌شدنِ آن در «مدیریت ماژول‌ها» این‌جا ساخته و هنگامِ غیرفعال‌شدن حذف می‌شود.
/// </summary>
public static class ModuleShortcuts
{
    private sealed record Target(string ShortcutName, string Exe, string? Args);

    // فقط ماژول‌هایی که میانبرِ مستقلِ معنادار دارند (لانچرِ جدا یا --goto به صفحهٔ خودشان).
    private static readonly Dictionary<string, Target> Map = new(System.StringComparer.OrdinalIgnoreCase)
    {
        [ModuleService.Pos]        = new("صندوقِ فروشِ سما حساب", "pos.exe", null),
        [ModuleService.Restaurant] = new("رستورانِ سما حساب", "restoran.exe", null),
        [ModuleService.Tourism]    = new("گردشگری — سما حساب", "SamaHesab.exe", "--goto=TourismDeposits"),
        [ModuleService.Hr]         = new("حقوق و دستمزدِ سما حساب", "SamaHesab.exe", "--goto=Salary"),
        [ModuleService.Support]    = new("مرکزِ پشتیبانیِ سما حساب", "SamaHesab.exe", "--goto=HelpCenter"),
    };

    private static string AppDir => System.AppDomain.CurrentDomain.BaseDirectory;
    private static string Desktop => System.Environment.GetFolderPath(System.Environment.SpecialFolder.DesktopDirectory);

    /// <summary>میانبرِ یک ماژول را مطابقِ فعال‌بودنش بساز/حذف کن (بی‌صدا؛ میانبر حیاتی نیست).</summary>
    public static void Sync(string moduleKey, bool enabled)
    {
        if (!Map.TryGetValue(moduleKey, out var t)) return;   // ماژولِ بدونِ میانبرِ مستقل
        var lnk = Path.Combine(Desktop, t.ShortcutName + ".lnk");
        try
        {
            if (enabled)
            {
                var exe = Path.Combine(AppDir, t.Exe);
                if (File.Exists(exe)) CreateShortcut(lnk, exe, t.Args, AppDir);
            }
            else if (File.Exists(lnk)) File.Delete(lnk);
        }
        catch { /* عدمِ ساختِ میانبر نباید جریانِ ماژول را بشکند */ }
    }

    /// <summary>در استارت‌آپ: برای ماژول‌های فعال میانبرِ گم‌شده ساخته و برای غیرفعال‌ها حذف می‌شود.</summary>
    public static void SyncAll(System.Collections.Generic.IEnumerable<string> enabledKeys)
    {
        var enabled = new System.Collections.Generic.HashSet<string>(enabledKeys, System.StringComparer.OrdinalIgnoreCase);
        foreach (var key in Map.Keys) Sync(key, enabled.Contains(key));
    }

    private static void CreateShortcut(string lnkPath, string targetExe, string? args, string workingDir)
    {
        var shellType = System.Type.GetTypeFromProgID("WScript.Shell");
        if (shellType is null) return;
        dynamic shell = System.Activator.CreateInstance(shellType)!;
        var sc = shell.CreateShortcut(lnkPath);
        sc.TargetPath = targetExe;
        sc.Arguments = args ?? "";
        sc.WorkingDirectory = workingDir;
        sc.IconLocation = targetExe + ",0";
        sc.Save();
    }
}
