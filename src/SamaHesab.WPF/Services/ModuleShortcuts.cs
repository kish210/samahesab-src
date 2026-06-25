using System.IO;
using System.Runtime.InteropServices;

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

    // ساختِ .lnk با IShellLinkW (یونیکدِ کامل) — برخلافِ WScript.Shell، نامِ فارسیِ فایل را درست ذخیره می‌کند.
    private static void CreateShortcut(string lnkPath, string targetExe, string? args, string workingDir)
    {
        var link = (IShellLinkW)new ShellLink();
        link.SetPath(targetExe);
        if (!string.IsNullOrEmpty(args)) link.SetArguments(args);
        link.SetWorkingDirectory(workingDir);
        link.SetIconLocation(targetExe, 0);
        ((IPersistFile)link).Save(lnkPath, false);
    }

    [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
    private class ShellLink { }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown),
     Guid("000214F9-0000-0000-C000-000000000046")]
    private interface IShellLinkW
    {
        void GetPath([MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszFile, int cch, System.IntPtr pfd, int fFlags);
        void GetIDList(out System.IntPtr ppidl);
        void SetIDList(System.IntPtr pidl);
        void GetDescription([MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszName, int cch);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
        void GetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszDir, int cch);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
        void GetArguments([MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszArgs, int cch);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
        void GetHotkey(out short pwHotkey);
        void SetHotkey(short wHotkey);
        void GetShowCmd(out int piShowCmd);
        void SetShowCmd(int iShowCmd);
        void GetIconLocation([MarshalAs(UnmanagedType.LPWStr)] System.Text.StringBuilder pszIconPath, int cch, out int piIcon);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, int dwReserved);
        void Resolve(System.IntPtr hwnd, int fFlags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown),
     Guid("0000010b-0000-0000-C000-000000000046")]
    private interface IPersistFile
    {
        void GetClassID(out System.Guid pClassID);
        [PreserveSig] int IsDirty();
        void Load([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, int dwMode);
        void Save([MarshalAs(UnmanagedType.LPWStr)] string pszFileName, [MarshalAs(UnmanagedType.Bool)] bool fRemember);
        void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string pszFileName);
        void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string ppszFileName);
    }
}
