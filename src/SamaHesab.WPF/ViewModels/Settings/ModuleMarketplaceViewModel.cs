using System.Collections.ObjectModel;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;

namespace SamaHesab.WPF.ViewModels.Settings;

/// <summary>
/// بازارِ ماژول‌ها — فهرستِ ماژول‌های آمادهٔ نصب را از releaseِ «modules»ِ مخزنِ عمومیِ
/// kish210/SamaHesab می‌خواند (همان جایی که نصاب‌ها هستند) و اجازهٔ دانلود/نصب می‌دهد.
/// منبعِ سورس جداست (samahesab-src)؛ این فقط بسته‌های کامپایل‌شده را می‌گیرد.
/// </summary>
public partial class ModuleMarketplaceViewModel : BaseViewModel
{
    private const string CatalogUrl = "https://github.com/kish210/SamaHesab/releases/download/modules/modules-catalog.json";
    private const string PackageBaseUrl = "https://github.com/kish210/SamaHesab/releases/download/modules/";

    private readonly ModuleService _modules;
    // نسخهٔ *در حالِ اجرا* per ماژول (از IModoleهای بارگذاری‌شده — شاملِ ماژولِ دانلودشدهٔ runtime).
    private readonly Dictionary<string, string> _runningVersions;
    private static readonly HttpClient _http = new() { Timeout = System.TimeSpan.FromSeconds(20) };

    [ObservableProperty] private string _status = string.Empty;

    public ObservableCollection<MarketModuleRow> Modules { get; } = new();

    public ModuleMarketplaceViewModel(ModuleService modules, IDialogService dialogService,
        INavigationService navigationService,
        IEnumerable<SamaHesab.Modules.Abstractions.IModule>? loadedModules = null)
        : base(dialogService, navigationService)
    {
        _modules = modules;
        _runningVersions = (loadedModules ?? Enumerable.Empty<SamaHesab.Modules.Abstractions.IModule>())
            .GroupBy(m => m.Key)
            .ToDictionary(g => g.Key, g => g.First().Version, System.StringComparer.OrdinalIgnoreCase);
    }

    private static string ModulesDir
    {
        get
        {
            var d = System.IO.Path.Combine(AppSettingsStore.AppDataDir, "modules");
            System.IO.Directory.CreateDirectory(d);
            return d;
        }
    }

    public override async Task LoadAsync() => await RefreshAsync();

    private sealed record CatalogDto(string? coreVersion, string? updatedAt, CatalogModule[]? modules);
    private sealed record CatalogModule(string key, string displayName, string version, string? schema,
        string? description, string package, int sizeKB, string? minCore);

    [RelayCommand]
    private async Task RefreshAsync()
    {
        // ۱) ماژول‌های محلی را *فوری* نشان بده (بدونِ انتظارِ شبکه) تا صفحه آفلاین هم بلافاصله کار کند.
        //    (پیش‌تر پرکردنِ ردیف‌ها پشتِ HTTPِ کاتالوگ بود؛ آفلاین تا ۲۰ثانیه «در حال بارگذاری» می‌ماند.)
        PopulateRows(catByKey: null, coreVer: null);

        // ۲) کاتالوگِ آنلاینِ بازار best-effort؛ موفق → ردیف‌ها با نسخه/توضیح/حجمِ دانلود غنی می‌شوند.
        await ExecuteAsync(async () =>
        {
            var catByKey = new System.Collections.Generic.Dictionary<string, CatalogModule>();
            string? coreVer = null;
            try
            {
                if (!_http.DefaultRequestHeaders.Contains("User-Agent"))
                    _http.DefaultRequestHeaders.Add("User-Agent", "SamaHesab");
                var cat = await _http.GetFromJsonAsync<CatalogDto>(CatalogUrl);
                coreVer = cat?.coreVersion;
                foreach (var m in cat?.modules ?? System.Array.Empty<CatalogModule>()) catByKey[m.key] = m;
            }
            catch { return; /* آفلاین — ردیف‌های محلیِ مرحلهٔ ۱ سرِجا می‌مانند */ }

            if (catByKey.Count > 0) PopulateRows(catByKey, coreVer);
        }, "بررسیِ بازارِ آنلاین...");
    }

    /// <summary>پرکردنِ فهرستِ ماژول‌ها از تعریف‌های محلی، با غنی‌سازیِ اختیاری از کاتالوگِ بازار.</summary>
    private void PopulateRows(System.Collections.Generic.IReadOnlyDictionary<string, CatalogModule>? catByKey, string? coreVer)
    {
        Modules.Clear();
        foreach (var def in _modules.OptionalModules)
        {
            CatalogModule? c = null;
            catByKey?.TryGetValue(def.Key, out c);
            var pkg = c?.package ?? (def.Key + ".mspkg");
            // نسخهٔ نمایش = نسخهٔ *در حالِ اجرا* (IModule)؛ برای ماژول‌های بدونِ IModule (Web/Mobile/پشتیبانی) از تعریفِ ماژول.
            var runningVersion = _runningVersions.GetValueOrDefault(def.Key) ?? def.Version;
            // اگر کاتالوگ نسخهٔ جدیدتری دارد، در دسترس بودنِ به‌روزرسانی با همان دکمهٔ Install دیده می‌شود.
            Modules.Add(new MarketModuleRow(def.Key, def.Name, runningVersion, c?.description ?? "", c?.sizeKB ?? 0, pkg)
            {
                Enabled = _modules.IsEnabled(def.Key),
                Installed = System.IO.File.Exists(System.IO.Path.Combine(ModulesDir, pkg)),
                InCatalog = c != null,
                CatalogVersion = c?.version,
            });
        }
        // AUDIT-2 — نسخهٔ هستهٔ در حالِ اجرا را نشان بده (نه coreVersionِ کهنهٔ کاتالوگ).
        var coreNow = SamaHesab.WPF.Services.AppVersion.Display;
        Status = catByKey is { Count: > 0 }
            ? $"{Modules.Count} ماژول · هستهٔ {coreNow}" + (string.IsNullOrWhiteSpace(coreVer) || coreVer == coreNow ? "" : $" (کاتالوگ: {coreVer})")
            : $"{Modules.Count} ماژول (مدیریتِ محلی فعال است)";
    }

    /// <summary>فعال/غیرفعال‌سازیِ ماژول (Enable/Disable) با کنترلِ تداخل.</summary>
    [RelayCommand]
    private async Task ToggleEnableAsync(MarketModuleRow? row)
    {
        if (row is null) return;
        var target = !row.Enabled;
        if (!_modules.TrySetEnabled(row.Key, target, out var err) && err is not null)
        { await _dialogService.ShowWarningAsync(err); return; }
        row.Enabled = _modules.IsEnabled(row.Key);
        ModuleShortcuts.Sync(row.Key, row.Enabled);   // میانبرِ دسکتاپ را با وضعیت هم‌گام کن
        // پیش‌نیازِ سرورِ API: اگر ماژولی نیازمندِ API فعال است، میانبرِ سرور را هم بساز/نگه‌دار.
        ModuleShortcuts.SyncApiServer(_modules.OptionalModules.Any(
            m => ModuleShortcuts.RequiresApi(m.Key) && _modules.IsEnabled(m.Key)));
        await _dialogService.ShowSuccessAsync(row.Enabled
            ? $"ماژولِ «{row.DisplayName}» فعال شد. منو/صفحاتش به‌روزرسانی می‌شوند."
            : $"ماژولِ «{row.DisplayName}» غیرفعال شد. (دادهٔ تاریخی حفظ می‌شود.)");

        // اعلامِ پیش‌نیاز: ماژول‌های تحتِ‌وب (پنلِ فروش) بدونِ اجرای «سرورِ سما حساب (API)» کار نمی‌کنند.
        if (row.Enabled && ModuleShortcuts.RequiresApi(row.Key))
            await _dialogService.ShowInfoAsync(
                $"ماژولِ «{row.DisplayName}» از طریقِ مرورگر کار می‌کند و **نیازمندِ سرورِ سما حساب (API)** است.\n" +
                "۱) مطمئن شوید سرور اجراست (میانبرِ «سرورِ سما حساب (API)» روی دسکتاپ ساخته شد).\n" +
                $"۲) سپس در مرورگر/موبایل به نشانیِ زیر بروید:\n{ModuleShortcuts.PanelUrl}\n" +
                "(برای دستگاه‌های دیگر روی شبکه: به‌جای localhost آدرسِ آی‌پیِ این سرور را بگذارید.)");
    }

    /// <summary>حذفِ ماژول (Remove): غیرفعال + پاک‌کردنِ بستهٔ دانلودشدهٔ محلی. دادهٔ DB حفظ می‌شود.</summary>
    [RelayCommand]
    private async Task RemoveAsync(MarketModuleRow? row)
    {
        if (row is null) return;
        if (!await _dialogService.ConfirmAsync(
            $"ماژولِ «{row.DisplayName}» غیرفعال و بستهٔ دانلودشده‌اش حذف شود؟ (دادهٔ تاریخیِ آن در پایگاه‌داده حفظ می‌ماند.)",
            "حذف ماژول")) return;
        _modules.SetEnabled(row.Key, false);
        row.Enabled = false;
        ModuleShortcuts.Sync(row.Key, false);   // میانبرِ دسکتاپِ ماژولِ حذف‌شده هم برداشته شود
        try
        {
            var pkg = System.IO.Path.Combine(ModulesDir, row.Package);
            if (System.IO.File.Exists(pkg)) System.IO.File.Delete(pkg);
            var ext = System.IO.Path.Combine(ModulesDir, System.IO.Path.GetFileNameWithoutExtension(row.Package));
            if (System.IO.Directory.Exists(ext)) System.IO.Directory.Delete(ext, true);
        }
        catch { /* حذفِ فایل best-effort */ }
        row.Installed = false;
        await _dialogService.ShowSuccessAsync($"ماژولِ «{row.DisplayName}» حذف شد.");
    }

    [RelayCommand]
    private async Task InstallAsync(MarketModuleRow? row)
    {
        if (row is null || row.IsDownloading) return;
        // U-SEC-3: row.Package از کاتالوگِ آنلاین (JSONِ ریلیزِ گیت‌هاب) می‌آید — اگر آن ریلیز روزی
        // compromise شود، یک package مثلِ "..\..\..\App.exe" می‌توانست بیرون از پوشهٔ ماژول‌ها بنویسد.
        // این‌جا فقط اجازهٔ نامِ فایلِ ساده (بدونِ جداکنندهٔ مسیر/".." ) داده می‌شود.
        var safeName = System.IO.Path.GetFileName(row.Package);
        if (string.IsNullOrWhiteSpace(safeName) || safeName != row.Package || row.Package.Contains(".."))
        {
            await _dialogService.ShowErrorAsync("نامِ بستهٔ ماژول نامعتبر است؛ نصب لغو شد.");
            return;
        }
        var dest = System.IO.Path.Combine(ModulesDir, row.Package);
        try
        {
            row.IsDownloading = true; row.Progress = 0; row.StatusText = "در حال دانلود...";

            // دانلودِ جریانی با گزارشِ درصدِ پیشرفت (نوارِ دانلودِ per-row).
            using var resp = await _http.GetAsync(PackageBaseUrl + row.Package, HttpCompletionOption.ResponseHeadersRead);
            resp.EnsureSuccessStatusCode();
            var total = resp.Content.Headers.ContentLength ?? (row.SizeKB * 1024L);
            await using var src = await resp.Content.ReadAsStreamAsync();
            await using var dst = System.IO.File.Create(dest);
            var buffer = new byte[81920];
            long read = 0; int n;
            while ((n = await src.ReadAsync(buffer)) > 0)
            {
                await dst.WriteAsync(buffer.AsMemory(0, n));
                read += n;
                row.Progress = total > 0 ? (int)(read * 100 / total) : 0;
                row.DownloadedText = $"{read / 1024} / {(total > 0 ? total / 1024 : row.SizeKB)} کیلوبایت";
            }
            row.Progress = 100;

            row.Installed = true;
            // U-SEC-3b: پیش‌تر نتیجهٔ TrySetEnabled نادیده گرفته می‌شد — اگر ماژول با ماژولِ فعالِ
            // دیگری تداخل داشت، دانلود موفق بود ولی فعال‌سازی بی‌صدا شکست می‌خورد و پیامِ «دانلود و
            // فعال شد» همچنان نشان داده می‌شد (برخلافِ ToggleEnableAsync که این را درست چک می‌کند).
            var enabled = _modules.TrySetEnabled(row.Key, true, out var conflictErr);
            row.Enabled = enabled && _modules.IsEnabled(row.Key);
            if (row.Enabled) ModuleShortcuts.Sync(row.Key, true);   // میانبرِ دسکتاپِ ماژولِ نصب‌شده
            row.StatusText = row.Enabled ? "✓ نصب شد" : "دانلود شد (غیرفعال)";
            if (row.Enabled)
                await _dialogService.ShowSuccessAsync(
                    $"ماژولِ «{row.DisplayName}» (نسخهٔ {row.Version}) دانلود و فعال شد. برای بارگذاریِ کاملِ ماژول، برنامه را یک‌بار ببندید و باز کنید.");
            else
                await _dialogService.ShowWarningAsync(
                    $"ماژولِ «{row.DisplayName}» دانلود شد ولی فعال نشد" + (conflictErr is null ? "." : $": {conflictErr}"));
        }
        catch (System.Exception ex)
        {
            try { if (System.IO.File.Exists(dest)) System.IO.File.Delete(dest); } catch { }
            row.StatusText = "خطا در دانلود";
            await _dialogService.ShowErrorAsync("دانلودِ ماژول ناموفق بود: " + ex.GetBaseException().Message);
        }
        finally { row.IsDownloading = false; }
    }
}

public partial class MarketModuleRow : ObservableObject
{
    public string Key { get; }
    public string DisplayName { get; }
    public string Version { get; }
    public string Description { get; }
    public int SizeKB { get; }
    public string Package { get; }
    [ObservableProperty] private bool _installed;
    [ObservableProperty] private bool _enabled;
    [ObservableProperty] private bool _inCatalog;
    /// <summary>نسخهٔ موجود در کاتالوگِ آنلاین (برای تشخیصِ به‌روزرسانی)؛ null اگر آفلاین/خارج از بازار.</summary>
    [ObservableProperty] private string? _catalogVersion;
    [ObservableProperty] private bool _isDownloading;
    [ObservableProperty] private int _progress;
    [ObservableProperty] private string _downloadedText = string.Empty;
    [ObservableProperty] private string _statusText = string.Empty;

    public MarketModuleRow(string key, string displayName, string version, string description, int sizeKB, string package)
    { Key = key; DisplayName = displayName; Version = version; Description = description; SizeKB = sizeKB; Package = package; }
}
