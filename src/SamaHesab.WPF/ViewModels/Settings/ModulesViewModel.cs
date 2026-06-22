using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.ObjectModel;
using System.Linq;

namespace SamaHesab.WPF.ViewModels.Settings;

/// <summary>تنظیمات → مدیریت ماژول‌ها: فعال/غیرفعال‌سازی ماژول‌های اختیاری پلتفرم.</summary>
public partial class ModulesViewModel : BaseViewModel
{
    private readonly ModuleService _modules;

    public ObservableCollection<ModuleToggle> OptionalModules { get; } = new();

    /// <summary>ماژول‌های هسته — همیشه فعال، غیرقابل‌خاموش‌کردن.</summary>
    public List<string> CoreModules { get; }

    public ModulesViewModel(ModuleService modules, IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    {
        _modules = modules;
        CoreModules = _modules.CoreModules.Select(m => m.Name).ToList();
        foreach (var m in _modules.OptionalModules)
            OptionalModules.Add(new ModuleToggle(m.Key, m.Name, _modules.IsEnabled(m.Key)));
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        // اول خاموش‌ها (تا تداخل با وضعیتِ نهایی سنجیده شود)، سپس روشن‌ها با کنترلِ تداخل.
        foreach (var t in OptionalModules.Where(t => !t.IsEnabled))
            _modules.SetEnabled(t.Key, false);

        var blocked = new List<string>();
        foreach (var t in OptionalModules.Where(t => t.IsEnabled))
            if (!_modules.TrySetEnabled(t.Key, true, out var err) && err is not null)
            {
                blocked.Add(err);
                t.IsEnabled = _modules.IsEnabled(t.Key);   // بازگرداندنِ تیک در UI
            }

        if (blocked.Count > 0)
            await _dialogService.ShowWarningAsync(
                "برخی ماژول‌ها به‌خاطرِ تداخل فعال نشدند:\n" + string.Join("\n", blocked));
        else
            await _dialogService.ShowSuccessAsync(
                "ماژول‌ها ذخیره شد. منوها و صفحات مطابق ماژول‌های فعال به‌روزرسانی می‌شوند.");
    }

    /// <summary>خروجیِ تنظیماتِ ماژول‌ها به فایل — برای انتقال به ماشینِ دیگر (یکپارچه‌سازیِ منوها).</summary>
    [RelayCommand]
    private async Task ExportAsync()
    {
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            FileName = "samahesab-modules.json",
            Filter = "تنظیماتِ ماژول (*.json)|*.json",
            Title = "خروجیِ تنظیماتِ ماژول",
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            foreach (var t in OptionalModules) _modules.SetEnabled(t.Key, t.IsEnabled);   // اول وضعیتِ جاری اعمال شود
            System.IO.File.WriteAllText(dlg.FileName, System.Text.Json.JsonSerializer.Serialize(
                _modules.GetEnabledKeys(), new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            await _dialogService.ShowSuccessAsync(
                $"تنظیماتِ ماژول ذخیره شد:\n{dlg.FileName}\nاین فایل را روی ماشینِ دیگر «ورودی» بگیرید تا منوها یکسان شوند.");
        }
        catch (System.Exception ex) { await _dialogService.ShowErrorAsync("خطا در خروجی: " + ex.Message); }
    }

    /// <summary>ورودیِ تنظیماتِ ماژول از فایلِ ماشینِ دیگر — منوها فوراً یکپارچه می‌شوند.</summary>
    [RelayCommand]
    private async Task ImportAsync()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "تنظیماتِ ماژول (*.json)|*.json|همه فایل‌ها (*.*)|*.*",
            Title = "ورودیِ تنظیماتِ ماژول",
        };
        if (dlg.ShowDialog() != true) return;
        try
        {
            var keys = System.Text.Json.JsonSerializer.Deserialize<string[]>(System.IO.File.ReadAllText(dlg.FileName))
                       ?? System.Array.Empty<string>();
            _modules.ReplaceEnabled(keys);
            foreach (var t in OptionalModules) t.IsEnabled = _modules.IsEnabled(t.Key);   // بازتاب در UI
            await _dialogService.ShowSuccessAsync("تنظیماتِ ماژول وارد شد. منوها و صفحات به‌روزرسانی شدند.");
        }
        catch (System.Exception ex) { await _dialogService.ShowErrorAsync("خطا در ورودی: " + ex.Message); }
    }
}

public partial class ModuleToggle : ObservableObject
{
    public string Key { get; }
    public string Title { get; }
    [ObservableProperty] private bool _isEnabled;

    public ModuleToggle(string key, string title, bool isEnabled)
    { Key = key; Title = title; _isEnabled = isEnabled; }
}
