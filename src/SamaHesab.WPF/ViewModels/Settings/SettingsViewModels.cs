using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.ObjectModel;

namespace SamaHesab.WPF.ViewModels.Settings;

public partial class BackupViewModel : BaseViewModel
{
    private readonly IBackupService _backupService;
    private readonly IPersianCalendarService _calendar;

    [ObservableProperty] private string _backupPath = @"C:\SamaHesabBackup";
    [ObservableProperty] private bool _autoBackup = true;
    [ObservableProperty] private int _backupIntervalDays = 1;
    [ObservableProperty] private string _lastBackupTime = "بارگذاری...";
    [ObservableProperty] private string? _restoreFilePath;
    [ObservableProperty] private string? _cloudBackupPath;   // پوشهٔ Google Drive (سینکِ ابری)

    public ObservableCollection<BackupInfo> BackupHistory { get; } = new();

    public BackupViewModel(IBackupService backupService, IPersianCalendarService calendar,
        IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    { _backupService = backupService; _calendar = calendar; }

    public override async Task LoadAsync()
    {
        CloudBackupPath = AppSettingsStore.GetGeneral().CloudBackupFolder;
        await ExecuteAsync(async () =>
        {
            var history = await _backupService.GetBackupHistoryAsync();
            BackupHistory.Clear();
            foreach (var h in history) BackupHistory.Add(h);
            LastBackupTime = BackupHistory.FirstOrDefault()?.CreatedAt.ToString("yyyy/MM/dd HH:mm") ?? "هنوز پشتیبانی نشده";
        }, "در حال بارگذاری...");
    }

    [RelayCommand]
    private async Task ManualBackupAsync()
    {
        var ok = await _dialogService.ConfirmAsync("آیا از پایگاه داده پشتیبان‌گیری شود؟", "پشتیبان‌گیری");
        if (!ok) return;
        await ExecuteAsync(async () =>
        {
            var path = await _backupService.BackupAsync(BackupPath);
            var cloudMsg = CopyToCloud(path);   // کپیِ ابری (Google Drive) در صورتِ تنظیم
            await LoadAsync();
            await _dialogService.ShowSuccessAsync($"پشتیبان‌گیری با موفقیت انجام شد.\nفایل: {path}{cloudMsg}");
        }, "در حال پشتیبان‌گیری...");
    }

    /// <summary>کپیِ فایلِ بکاپ در پوشهٔ ابری (Google Drive for Desktop) در صورتِ تنظیم. پیامِ نتیجه را برمی‌گرداند.</summary>
    private string CopyToCloud(string backupFile)
    {
        var folder = (CloudBackupPath ?? "").Trim();
        if (string.IsNullOrWhiteSpace(folder)) return "";
        try
        {
            System.IO.Directory.CreateDirectory(folder);
            var dest = System.IO.Path.Combine(folder, System.IO.Path.GetFileName(backupFile));
            System.IO.File.Copy(backupFile, dest, overwrite: true);
            return $"\n☁ نسخهٔ ابری (Google Drive): {dest}";
        }
        catch (System.Exception ex) { return "\n⚠ کپیِ ابری ناموفق: " + ex.Message; }
    }

    /// <summary>تشخیصِ خودکارِ پوشهٔ Google Drive for Desktop و ذخیرهٔ آن به‌عنوانِ مقصدِ بکاپِ ابری.</summary>
    [RelayCommand]
    private async Task DetectGoogleDriveAsync()
    {
        var candidates = new[]
        {
            System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "Google Drive"),
            System.IO.Path.Combine(System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile), "My Drive"),
            @"G:\My Drive", @"G:\", @"H:\My Drive",
        };
        var found = candidates.FirstOrDefault(System.IO.Directory.Exists);
        if (found == null) { await _dialogService.ShowWarningAsync("پوشهٔ Google Drive یافت نشد. اگر Google Drive for Desktop نصب است، مسیرِ آن را دستی وارد کنید."); return; }
        CloudBackupPath = System.IO.Path.Combine(found, "SamaHesab-Backup");
        SaveCloud();
        await _dialogService.ShowSuccessAsync($"پوشهٔ Google Drive یافت شد:\n{CloudBackupPath}\nاز این پس هر بکاپ این‌جا هم کپی می‌شود.");
    }

    private void SaveCloud()
    {
        var g = AppSettingsStore.GetGeneral();
        g.CloudBackupFolder = CloudBackupPath;
        AppSettingsStore.SaveGeneral(g);
    }

    partial void OnCloudBackupPathChanged(string? value) => SaveCloud();

    [RelayCommand]
    private async Task BrowseRestoreFileAsync()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Backup Files (*.bak)|*.bak|All Files (*.*)|*.*",
            Title = "انتخاب فایل پشتیبان"
        };
        if (dlg.ShowDialog() == true) RestoreFilePath = dlg.FileName;
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task RestoreAsync()
    {
        if (string.IsNullOrWhiteSpace(RestoreFilePath)) { await _dialogService.ShowErrorAsync("فایل پشتیبان انتخاب کنید."); return; }
        var ok = await _dialogService.ConfirmAsync(
            "⚠ هشدار: بازیابی پایگاه داده اطلاعات فعلی را بازنویسی می‌کند.\nآیا ادامه می‌دهید؟", "بازیابی پایگاه داده");
        if (!ok) return;
        await ExecuteAsync(async () =>
        {
            await _backupService.RestoreAsync(RestoreFilePath);
            await _dialogService.ShowSuccessAsync("پایگاه داده با موفقیت بازیابی شد. لطفاً برنامه را مجدداً راه‌اندازی کنید.");
        }, "در حال بازیابی... لطفاً صبر کنید");
    }

    [RelayCommand]
    private async Task BrowseBackupPathAsync()
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "مسیر پشتیبان‌گیری را انتخاب کنید" };
        if (dlg.ShowDialog() == true) BackupPath = dlg.FolderName;
        await Task.CompletedTask;
    }
}

// Settings ViewModel — تنظیماتِ واقعی (ذخیره/بارگذاری از settings.user.json) + نسخه/به‌روزرسانی
public partial class SettingsViewModel : BaseViewModel
{
    private readonly UpdateService _updater;

    [ObservableProperty] private string _selectedTheme = "Sama";
    [ObservableProperty] private bool _compactMode;   // حالتِ فشردهٔ رابط (چگالی)
    [ObservableProperty] private string _selectedCurrency = "ریال";
    [ObservableProperty] private bool _smsEnabled;
    [ObservableProperty] private string _smsProvider = "kavenegar";
    [ObservableProperty] private string _smsApiKey = string.Empty;
    [ObservableProperty] private string _smsSender = string.Empty;
    [ObservableProperty] private string _companyName = string.Empty;
    [ObservableProperty] private string _companyPhone = string.Empty;
    [ObservableProperty] private string _companyNationalId = string.Empty;
    [ObservableProperty] private string _companyEconomicCode = string.Empty;
    [ObservableProperty] private string _companyRegNumber = string.Empty;
    [ObservableProperty] private string _companyAddress = string.Empty;
    [ObservableProperty] private string _companyPostalCode = string.Empty;
    [ObservableProperty] private string _companyEmail = string.Empty;
    [ObservableProperty] private string _companyWebsite = string.Empty;
    [ObservableProperty] private string _fiscalYearStart = string.Empty;
    [ObservableProperty] private string _fiscalYearEnd = string.Empty;
    [ObservableProperty] private decimal _defaultVatRate = 9;
    [ObservableProperty] private int _decimalPlaces;
    [ObservableProperty] private string _voucherPrefix = string.Empty;
    [ObservableProperty] private string _invoicePrefix = string.Empty;
    [ObservableProperty] private bool _autoBackupEnabled;
    [ObservableProperty] private int _backupIntervalDays = 1;
    [ObservableProperty] private string _backupPath = string.Empty;
    [ObservableProperty] private int _idleTimeoutMinutes;   // X6 — خروجِ خودکار پس از بی‌فعالیتی (۰=خاموش)
    [ObservableProperty] private string _updateStatus = string.Empty;

    public string AppVersionText => $"نسخهٔ برنامه: {AppVersion.Display}";

    public List<string> Themes { get; } = new() { "Sama" };
    public List<string> Currencies { get; } = new() { "ریال", "تومان" };
    public List<string> SmsProviders { get; } = new() { "kavenegar", "farazsms", "melipayamak" };

    public SettingsViewModel(UpdateService updater, IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService) { _updater = updater; }

    // تغییرِ سوییچِ «حالتِ فشرده» → اعمالِ زنده روی کلِ رابط + ذخیرهٔ ترجیح (بدونِ نیاز به دکمهٔ ذخیره).
    partial void OnCompactModeChanged(bool value)
    {
        DensityManager.Apply(value);
        AppSettingsStore.SaveCompactMode(value);
    }

    public override async Task LoadAsync()
    {
        var g = AppSettingsStore.GetGeneral();
        SelectedTheme = AppSettingsStore.GetTheme();
        CompactMode = AppSettingsStore.GetCompactMode();
        CompanyName = g.CompanyName ?? "";
        CompanyPhone = g.CompanyPhone ?? "";
        CompanyNationalId = g.CompanyNationalId ?? "";
        CompanyEconomicCode = g.CompanyEconomicCode ?? "";
        CompanyRegNumber = g.CompanyRegNumber ?? "";
        CompanyAddress = g.CompanyAddress ?? "";
        CompanyPostalCode = g.CompanyPostalCode ?? "";
        CompanyEmail = g.CompanyEmail ?? "";
        CompanyWebsite = g.CompanyWebsite ?? "";
        FiscalYearStart = string.IsNullOrWhiteSpace(g.FiscalYearStart) ? "1404/01/01" : g.FiscalYearStart!;
        FiscalYearEnd = string.IsNullOrWhiteSpace(g.FiscalYearEnd) ? "1404/12/29" : g.FiscalYearEnd!;
        SelectedCurrency = g.Currency;
        DefaultVatRate = g.DefaultVatRate;
        DecimalPlaces = g.DecimalPlaces;
        VoucherPrefix = g.VoucherPrefix; InvoicePrefix = g.InvoicePrefix;
        AutoBackupEnabled = g.AutoBackupEnabled; BackupIntervalDays = g.BackupIntervalDays;
        BackupPath = string.IsNullOrWhiteSpace(g.BackupPath) ? @"C:\SamaHesabBackup" : g.BackupPath!;
        IdleTimeoutMinutes = g.IdleTimeoutMinutes;
        SmsEnabled = g.SmsEnabled; SmsProvider = g.SmsProvider;
        SmsApiKey = g.SmsApiKey ?? ""; SmsSender = g.SmsSender ?? "";
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        await ExecuteAsync(async () =>
        {
            AppSettingsStore.SaveTheme(SelectedTheme);
            ThemeManager.Apply(SelectedTheme);
            // merge روی تنظیماتِ موجود — تا فیلدهای فهرست‌نشده (SetupCompleted/CloudBackupFolder/CompanyLogoPath)
            // پاک نشوند (باگِ قبلی: ساختِ نمونهٔ تازه → بازگشتِ ویزاردِ راه‌اندازی + از‌دست‌رفتنِ مسیرِ بکاپِ ابری).
            var g = AppSettingsStore.GetGeneral();
            g.CompanyName = CompanyName; g.CompanyPhone = CompanyPhone;
            g.CompanyNationalId = CompanyNationalId; g.CompanyEconomicCode = CompanyEconomicCode;
            g.CompanyRegNumber = CompanyRegNumber; g.CompanyAddress = CompanyAddress;
            g.CompanyPostalCode = CompanyPostalCode; g.CompanyEmail = CompanyEmail; g.CompanyWebsite = CompanyWebsite;
            g.FiscalYearStart = FiscalYearStart; g.FiscalYearEnd = FiscalYearEnd;
            g.Currency = SelectedCurrency; g.DefaultVatRate = DefaultVatRate; g.DecimalPlaces = DecimalPlaces;
            g.VoucherPrefix = VoucherPrefix; g.InvoicePrefix = InvoicePrefix;
            g.AutoBackupEnabled = AutoBackupEnabled; g.BackupIntervalDays = BackupIntervalDays; g.BackupPath = BackupPath;
            g.IdleTimeoutMinutes = IdleTimeoutMinutes;
            g.SmsEnabled = SmsEnabled; g.SmsProvider = SmsProvider;
            g.SmsApiKey = SmsApiKey; g.SmsSender = SmsSender;
            AppSettingsStore.SaveGeneral(g);
            await _dialogService.ShowSuccessAsync("تنظیمات ذخیره شد.");
        }, "در حال ذخیرهٔ تنظیمات...");
    }

    [RelayCommand]
    private async Task TestSmsAsync()
    {
        if (!SmsEnabled) { await _dialogService.ShowWarningAsync("SMS فعال نیست."); return; }
        await _dialogService.ShowInfoAsync("ارسالِ پیامکِ آزمایشی از طریقِ درگاهِ سرور انجام می‌شود (تنظیماتِ درگاه روی سرور).");
    }

    // ── نسخه و به‌روزرسانی ──
    [RelayCommand]
    private async Task CheckUpdateAsync()
    {
        await ExecuteAsync(async () =>
        {
            UpdateStatus = "در حال بررسی...";
            var info = await _updater.CheckAsync();
            if (info is null) { UpdateStatus = $"به‌روزرسانی‌ای موجود نیست (نسخهٔ فعلی {AppVersion.Display})."; return; }
            UpdateStatus = $"نسخهٔ جدید: {info.Tag}";
            if (!await _dialogService.ConfirmAsync($"نسخهٔ جدید ({info.Tag}) موجود است. اکنون دانلود و نصب شود؟ برنامه بسته می‌شود.", "به‌روزرسانی")) return;
            if (await _updater.DownloadAndRunAsync(info))
                System.Windows.Application.Current.Shutdown();
            else
                await _dialogService.ShowErrorAsync("دانلودِ به‌روزرسانی ناموفق بود.");
        }, "بررسی به‌روزرسانی...");
    }

    // ── ناوبریِ سریع به صفحاتِ تنظیماتِ تخصصی ──
    [RelayCommand] private void OpenSecurity() => _navigationService.NavigateTo("Security");
    [RelayCommand] private void OpenBackup() => _navigationService.NavigateTo("Backup");
    [RelayCommand] private void OpenModules() => _navigationService.NavigateTo("Modules");
    [RelayCommand] private void OpenBranches() => _navigationService.NavigateTo("Branches");
}

// Company Settings
public partial class CompanySettingsViewModel : BaseViewModel
{
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string? _nationalId;
    [ObservableProperty] private string? _economicCode;
    [ObservableProperty] private string? _address;
    [ObservableProperty] private string? _phone;

    public CompanySettingsViewModel(IDialogService d, INavigationService n) : base(d, n) { }
    public override async Task LoadAsync() => await Task.CompletedTask;

    [RelayCommand]
    private async Task SaveAsync() =>
        await ExecuteAsync(async () => await _dialogService.ShowSuccessAsync("اطلاعات شرکت ذخیره شد."));
}

// User Management
public partial class UserManagementViewModel : BaseViewModel
{
    public ObservableCollection<UserRow> Users { get; } = new();

    public UserManagementViewModel(IDialogService d, INavigationService n) : base(d, n) { }

    public override async Task LoadAsync()
    {
        Users.Clear();
        Users.Add(new UserRow(1, "admin", "مدیر سیستم", "مدیر سیستم", true, null));
        Users.Add(new UserRow(2, "accountant", "حسابدار اصلی", "حسابدار", true, null));
        await Task.CompletedTask;
    }

    [RelayCommand] private void AddNew() => _ = _dialogService.ShowInfoAsync("فرم ایجاد کاربر جدید");
    [RelayCommand] private void Edit(UserRow? u) => _ = _dialogService.ShowInfoAsync($"ویرایش کاربر: {u?.FullName}");

    [RelayCommand]
    private async Task ToggleActiveAsync(UserRow? u)
    {
        if (u == null) return;
        await _dialogService.ShowSuccessAsync($"وضعیت کاربر {u.FullName} تغییر کرد.");
    }
}

public record UserRow(int Id, string Username, string FullName, string RoleName, bool IsActive, string? LastLogin);

