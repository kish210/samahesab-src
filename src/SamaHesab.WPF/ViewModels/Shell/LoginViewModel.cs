using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Security.Commands;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.ObjectModel;
using System.Windows;

namespace SamaHesab.WPF.ViewModels.Shell;

public partial class LoginViewModel : ObservableObject
{
    [ObservableProperty] private string _username = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private int _selectedCompanyId;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private bool _hasError;
    [ObservableProperty] private string _loginButtonText = "ورود به سیستم";
    [ObservableProperty] private bool _isNotLoading = true;

    /// <summary>When true, authenticate through the Web API (POS/restaurant clients) instead of the DB.</summary>
    [ObservableProperty] private bool _isApiMode;

    /// <summary>Which program this login belongs to: «حسابداری» / «فروشگاه» / «رستوران».</summary>
    [ObservableProperty] private string _moduleName = "حسابداری";

    /// <summary>Raised on successful login in API mode; the host opens the POS/restaurant window.</summary>
    public event Action? Authenticated;

    public ObservableCollection<CompanyItem> Companies { get; } = new();

    private readonly IDialogService _dialogService;
    private readonly ICurrentUserService _currentUser;
    private readonly IMediator _mediator;
    private readonly ApiClient _apiClient;

    public LoginViewModel(IDialogService dialogService, ICurrentUserService currentUser, IMediator mediator, ApiClient apiClient)
    {
        _dialogService = dialogService;
        _currentUser = currentUser;
        _mediator = mediator;
        _apiClient = apiClient;
        LoadCompanies();
    }

    /// <summary>Switch this login form into Web API mode (used by pos.exe / restoran.exe).</summary>
    /// <param name="moduleName">Program label shown on the form, e.g. «فروشگاه» or «رستوران».</param>
    public void EnableApiMode(string moduleName)
    {
        IsApiMode = true;
        ModuleName = moduleName;
        var s = AppSettingsStore.GetApiSettings();
        if (string.IsNullOrWhiteSpace(Username)) Username = s.Username;
    }

    private void LoadCompanies()
    {
        Companies.Clear();
        // In production load from DB
        Companies.Add(new CompanyItem(1, "شرکت اول", "DEFAULT"));
        SelectedCompanyId = 1;
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (string.IsNullOrWhiteSpace(Username))
        { HasError = true; ErrorMessage = "نام کاربری را وارد کنید."; return; }
        if (string.IsNullOrWhiteSpace(Password))
        { HasError = true; ErrorMessage = "رمز عبور را وارد کنید."; return; }

        IsLoading = true; IsNotLoading = false; LoginButtonText = "در حال ورود..."; HasError = false;

        try
        {
            // ── API mode (POS / restaurant clients): authenticate over HTTP, never the DB ──
            if (IsApiMode)
            {
                var (ok, err) = await _apiClient.LoginAsync(Username, Password, SelectedCompanyId);
                if (!ok) { HasError = true; ErrorMessage = err ?? "ورود ناموفق بود."; return; }

                var me = await _apiClient.GetMeAsync();
                ((CurrentUserService)_currentUser).SetCurrentUser(
                    me?.UserId ?? 1, me?.CompanyId ?? SelectedCompanyId, me?.BranchId ?? 1,
                    me?.Username ?? Username, me?.FullName ?? Username,
                    me?.Roles ?? new[] { "ADMIN" }, me?.Permissions ?? Array.Empty<string>(),
                    me?.SalespersonPartyId);

                Authenticated?.Invoke();
                return;
            }

            int userId = 1; int branchId = 1; string fullName = Username;
            int? sellerPartyId = null;
            bool mustChangePass = false;
            List<string> roles = new(); List<string> permissions = new();

            try
            {
                // DB-backed authentication (Sec.Users, PBKDF2 + audit log).
                var result = await _mediator.Send(new AuthenticateCommand(SelectedCompanyId, Username, Password));
                if (result.Succeeded && result.Value is not null)
                {
                    userId = result.Value.UserId; branchId = result.Value.BranchId;
                    fullName = result.Value.FullName; roles = result.Value.Roles.ToList();
                    permissions = result.Value.Permissions.ToList();
                    sellerPartyId = result.Value.SalespersonPartyId;   // SP-1
                    mustChangePass = result.Value.MustChangePass;      // SR-6
                }
                else { HasError = true; ErrorMessage = result.ErrorMessage; return; }
            }
            catch
            {
                // عدمِ دسترسی به پایگاه‌داده = ورود ناموفق. (پیش‌تر اینجا یک درِ پشتیِ ثابت
                // `admin/admin123`|`admin/1234` بود که با قطعِ DB هر رمزِ عوض‌شده را دور می‌زد —
                // ریسکِ امنیتیِ فروش. حالا بدونِ DB هیچ ورودی پذیرفته نمی‌شود.)
                HasError = true;
                ErrorMessage = "عدم دسترسی به پایگاه داده؛ ورود ممکن نیست. اتصال به سرور را بررسی کنید.";
                return;
            }

            // SR-6: اگر این کاربر هنوز رمزِ پیش‌فرض دارد (مثلِ ادمینِ تازه‌ساخته‌شده)، پیش از ورود
            // به سیستم، تغییرِ رمز اجباری است. کاربر نمی‌تواند این پنجره را دور بزند.
            if (mustChangePass)
            {
                var win = new Views.Shell.ForcePasswordChangeWindow(_mediator, userId);
                var changed = win.ShowDialog() == true && win.PasswordChanged;
                if (!changed)
                {
                    HasError = true; ErrorMessage = "برای ادامه باید رمزِ عبور را تغییر دهید.";
                    return;
                }
            }

            ((CurrentUserService)_currentUser).SetCurrentUser(userId, SelectedCompanyId, branchId, Username,
                fullName, roles, permissions, sellerPartyId);

            // یادداشت: ویزاردِ راه‌اندازیِ اولیه از این‌جا حذف شد و حالا **پیش از لاگین** (در App.OnStartup) اجرا می‌شود
            //   تا کاربر اطلاعاتِ شرکت/دادهٔ پایه را قبل از ورود وارد کند (یک‌بار، بر اساسِ SetupCompleted).

            // فاز ۱۲ P-G7 — گیتِ لایسنس: اگر تریال منقضی/لایسنس نامعتبر بود، پنجرهٔ فعال‌سازی (مسدودکننده).
            try
            {
                var lic = App.GetService<Services.LicenseService>();
                var st = lic.GetStatus();
                if (st.State is Services.AppLicenseState.TrialExpired or Services.AppLicenseState.Expired
                        or Services.AppLicenseState.Invalid)
                {
                    new Views.Licensing.LicenseActivationWindow(
                        App.GetService<Licensing.LicenseActivationViewModel>()).ShowDialog();
                    if (!lic.GetStatus().CanRun)   // هنوز فعال نشد → ورود مسدود
                    {
                        HasError = true; ErrorMessage = "برای ادامه باید نرم‌افزار را فعال کنید.";
                        return;
                    }
                }
            }
            catch { /* خطای لایسنس نباید ورود را در حالتِ سالم بشکند */ }

            var mainWindow = App.GetService<Views.Shell.MainWindow>();
            mainWindow.Show();

            foreach (Window w in System.Windows.Application.Current.Windows)
                if (w is Views.Shell.LoginWindow) { w.Close(); break; }
        }
        finally
        {
            IsLoading = false; IsNotLoading = true; LoginButtonText = "ورود به سیستم";
        }
    }
}

public record CompanyItem(int Id, string Name, string Code);
