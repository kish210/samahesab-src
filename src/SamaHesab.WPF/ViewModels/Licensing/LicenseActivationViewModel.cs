using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SamaHesab.Application.Support;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;

namespace SamaHesab.WPF.ViewModels.Licensing;

/// <summary>
/// فاز ۱۲ P-G7 — پنجرهٔ فعال‌سازی: نمایشِ اثرِانگشتِ دستگاه (مشتری برای وندور می‌فرستد) +
/// دو راهِ فعال‌سازی: (آفلاین) بارگذاریِ فایلِ لایسنسِ امضاشده · (آنلاین) فعال‌سازی از طریقِ سایتِ پشتیبانی.
/// </summary>
public partial class LicenseActivationViewModel : BaseViewModel
{
    private readonly LicenseService _license;
    private readonly ISupportApiClient _support;

    [ObservableProperty] private string _fingerprint = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isActivated;
    [ObservableProperty] private bool _canContinue;
    [ObservableProperty] private bool _isBusy;

    public event System.Action? Finished;

    public LicenseActivationViewModel(LicenseService license, ISupportApiClient support,
        IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    {
        _license = license;
        _support = support;
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

    /// <summary>فعال‌سازی از طریقِ سایتِ پشتیبانی (آنلاین): اعلامِ نصب → دریافتِ تأیید/انقضا و ذخیرهٔ محلی.</summary>
    [RelayCommand]
    private async System.Threading.Tasks.Task ActivateOnlineAsync()
    {
        var sup = AppSettingsStore.GetSupport();
        if (string.IsNullOrWhiteSpace(sup.BaseUrl))
        {
            await _dialogService.ShowWarningAsync("ابتدا آدرسِ سرورِ پشتیبانی را در «تنظیمات → لایسنس و پشتیبانی» وارد کنید.");
            return;
        }
        IsBusy = true;
        try
        {
            var g = AppSettingsStore.GetGeneral();
            var info = new InstallInfo(_license.MachineFingerprint, g.CompanyName ?? "",
                g.BusinessType ?? "", AppVersion.Display);
            var r = await _support.RegisterInstallAsync(info);
            if (!r.Succeeded || r.Value is null)
            {
                await _dialogService.ShowErrorAsync("اتصال به سایت ناموفق بود: " + (r.ErrorMessage ?? "نامشخص"));
                return;
            }
            var s = r.Value;
            if (!s.Approved)
            {
                await _dialogService.ShowInfoAsync(
                    "نصبِ شما به سایت اعلام شد و در انتظارِ تأییدِ مدیر است. پس از تأیید، دوباره «فعال‌سازی از طریقِ سایت» را بزنید.");
                return;
            }
            // تأییدشده: کلید-API/لایسنس و انقضای سروری را محلی ذخیره کن (هم‌مسیر با اعلامِ خودکارِ استارت‌آپ).
            if (!string.IsNullOrWhiteSpace(s.ApiKey))
            {
                sup.ApiKey = s.ApiKey!; sup.CustomerId = _license.MachineFingerprint;
                if (!string.IsNullOrWhiteSpace(s.LicenseId)) sup.LicenseId = s.LicenseId!;
                AppSettingsStore.SaveSupport(sup);
            }
            if (!string.IsNullOrWhiteSpace(s.Expiry) && System.DateTime.TryParse(s.Expiry, out var exp))
            {
                g.ServerLicenseExpiryUtc = exp.ToUniversalTime().ToString("o");
                g.ServerLicenseTier = s.LicenseId;
                AppSettingsStore.SaveGeneral(g);
            }
            Refresh();
            await _dialogService.ShowSuccessAsync("فعال‌سازی از طریقِ سایت انجام شد. " + StatusMessage);
        }
        catch (System.Exception ex) { await _dialogService.ShowErrorAsync("خطا در فعال‌سازیِ آنلاین: " + ex.Message); }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async System.Threading.Tasks.Task CopyFingerprintAsync()
    {
        try { System.Windows.Clipboard.SetText(Fingerprint); await _dialogService.ShowInfoAsync("شناسهٔ دستگاه کپی شد."); }
        catch { }
    }

    [RelayCommand] private void Continue() => Finished?.Invoke();
}
