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
    private readonly IModuleService _modules;

    public ObservableCollection<ModuleToggle> OptionalModules { get; } = new();

    /// <summary>ماژول‌های هسته — همیشه فعال، غیرقابل‌خاموش‌کردن.</summary>
    public List<string> CoreModules { get; } = new()
    {
        "حسابداری", "خزانه‌داری", "فروش", "خرید", "انبار", "اشخاص (مشتری/تأمین‌کننده)", "گزارش‌ها"
    };

    public ModulesViewModel(IModuleService modules, IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    {
        _modules = modules;
        foreach (var m in _modules.OptionalModules)
            OptionalModules.Add(new ModuleToggle(m.Key, m.Title, _modules.IsEnabled(m.Key)));
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        foreach (var t in OptionalModules)
            _modules.SetEnabled(t.Key, t.IsEnabled);
        _modules.Save();
        await _dialogService.ShowSuccessAsync(
            "ماژول‌ها ذخیره شد. منوها و صفحات مطابق ماژول‌های فعال به‌روزرسانی می‌شوند.");
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
