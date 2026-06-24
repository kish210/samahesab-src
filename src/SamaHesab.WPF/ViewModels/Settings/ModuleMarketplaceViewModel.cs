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
    private static readonly HttpClient _http = new() { Timeout = System.TimeSpan.FromSeconds(20) };

    [ObservableProperty] private string _status = string.Empty;

    public ObservableCollection<MarketModuleRow> Modules { get; } = new();

    public ModuleMarketplaceViewModel(ModuleService modules, IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    { _modules = modules; }

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
        await ExecuteAsync(async () =>
        {
            Modules.Clear();
            CatalogDto? cat;
            try
            {
                if (!_http.DefaultRequestHeaders.Contains("User-Agent"))
                    _http.DefaultRequestHeaders.Add("User-Agent", "SamaHesab");
                cat = await _http.GetFromJsonAsync<CatalogDto>(CatalogUrl);
            }
            catch (System.Exception ex)
            {
                Status = "خطا در دریافتِ فهرستِ بازار (اتصالِ اینترنت؟): " + ex.GetBaseException().Message;
                return;
            }
            if (cat?.modules is null || cat.modules.Length == 0) { Status = "ماژولی در بازار موجود نیست."; return; }

            foreach (var m in cat.modules)
            {
                var installed = System.IO.File.Exists(System.IO.Path.Combine(ModulesDir, m.package));
                Modules.Add(new MarketModuleRow(m.key, m.displayName, m.version, m.description ?? "", m.sizeKB, m.package)
                {
                    Installed = installed,
                    Enabled = _modules.IsEnabled(m.key),
                });
            }
            Status = $"{Modules.Count} ماژول در بازار · هستهٔ سازگار {cat.coreVersion}";
        }, "در حال دریافتِ فهرستِ بازار...");
    }

    [RelayCommand]
    private async Task InstallAsync(MarketModuleRow? row)
    {
        if (row is null || row.IsDownloading) return;
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
            _modules.TrySetEnabled(row.Key, true, out _);   // فعال‌سازی در ModuleService
            row.Enabled = true;
            row.StatusText = "✓ نصب شد";
            await _dialogService.ShowSuccessAsync(
                $"ماژولِ «{row.DisplayName}» (نسخهٔ {row.Version}) دانلود و فعال شد. برای بارگذاریِ کاملِ ماژول، برنامه را یک‌بار ببندید و باز کنید.");
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
    [ObservableProperty] private bool _isDownloading;
    [ObservableProperty] private int _progress;
    [ObservableProperty] private string _downloadedText = string.Empty;
    [ObservableProperty] private string _statusText = string.Empty;

    public MarketModuleRow(string key, string displayName, string version, string description, int sizeKB, string package)
    { Key = key; DisplayName = displayName; Version = version; Description = description; SizeKB = sizeKB; Package = package; }
}
