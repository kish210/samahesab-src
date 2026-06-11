using CommunityToolkit.Mvvm.ComponentModel;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.ObjectModel;

namespace SamaHesab.WPF.ViewModels.Settings;

/// <summary>مدیریت ماژول‌ها (تنظیمات → ماژول‌ها): روشن/خاموش کردن ماژول‌های اختیاری.</summary>
public partial class ModuleSettingsViewModel : BaseViewModel
{
    private readonly ModuleService _modules;

    public ObservableCollection<ModuleRow> CoreModules { get; } = new();
    public ObservableCollection<ModuleRow> OptionalModules { get; } = new();

    public ModuleSettingsViewModel(ModuleService modules, IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    {
        _modules = modules;
        foreach (var m in _modules.CoreModules)
            CoreModules.Add(new ModuleRow(m, true, null));
        foreach (var m in _modules.OptionalModules)
            OptionalModules.Add(new ModuleRow(m, _modules.IsEnabled(m.Key), OnToggle));
    }

    private void OnToggle(ModuleRow row) => _modules.SetEnabled(row.Key, row.IsOn);
}

public partial class ModuleRow : ObservableObject
{
    public string Key { get; }
    public string Name { get; }
    public string Icon { get; }
    public bool Core { get; }
    private readonly Action<ModuleRow>? _onChanged;
    [ObservableProperty] private bool _isOn;

    public ModuleRow(ModuleDef def, bool isOn, Action<ModuleRow>? onChanged)
    { Key = def.Key; Name = def.Name; Icon = def.Icon; Core = def.Core; _isOn = isOn; _onChanged = onChanged; }

    partial void OnIsOnChanged(bool value) => _onChanged?.Invoke(this);
}
