using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;

namespace SamaHesab.WPF.ViewModels.Licensing;

/// <summary>
/// فاز ۱۲ P-G7 — پنجرهٔ فعال‌سازی: نمایشِ اثرِانگشتِ دستگاه (مشتری برای وندور می‌فرستد) +
/// بارگذاریِ فایلِ لایسنسِ امضاشده. کاملاً آفلاین.
/// </summary>
public partial class LicenseActivationViewModel : BaseViewModel
{
    private readonly LicenseService _license;

    [ObservableProperty] private string _fingerprint = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isActivated;
    [ObservableProperty] private bool _canContinue;

    public event System.Action? Finished;

    public LicenseActivationViewModel(LicenseService license, IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    {
        _license = license;
        Refresh();
    }

    public override System.Threading.Tasks.Task LoadAsync() => System.Threading.Tasks.Task.CompletedTask;

    private void Refresh()
    {
        Fingerprint = _license.MachineFingerprint;
        var s = _license.GetStatus();
        StatusMessage = s.Message;
        IsActivated = s.State == AppLicenseState.Activated;
        CanContinue = s.CanRun;   // در تریال یا فعال‌شده، اجازهٔ ادامه
    }

    /// <summary>از code-behind پس از انتخابِ فایلِ .lic صدا زده می‌شود.</summary>
    public async System.Threading.Tasks.Task InstallFromFileAsync(string path)
    {
        var (ok, msg) = _license.InstallLicense(path);
        if (ok) { await _dialogService.ShowSuccessAsync("لایسنس با موفقیت فعال شد. " + msg); Refresh(); }
        else await _dialogService.ShowErrorAsync("فعال‌سازی ناموفق: " + msg);
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task CopyFingerprintAsync()
    {
        try { System.Windows.Clipboard.SetText(Fingerprint); await _dialogService.ShowInfoAsync("شناسهٔ دستگاه کپی شد."); }
        catch { }
    }

    [RelayCommand] private void Continue() => Finished?.Invoke();
}
