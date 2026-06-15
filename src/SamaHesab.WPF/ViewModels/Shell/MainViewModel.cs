using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Dashboard;
using SamaHesab.WPF.ViewModels.Accounting;
using SamaHesab.WPF.ViewModels.Inventory;
using SamaHesab.WPF.ViewModels.Sales;
using SamaHesab.WPF.ViewModels.Purchase;
using SamaHesab.WPF.ViewModels.POS;
using SamaHesab.WPF.ViewModels.CRM;
using SamaHesab.WPF.ViewModels.HRM;
using SamaHesab.WPF.ViewModels.Reports;
using SamaHesab.WPF.ViewModels.Settings;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;

namespace SamaHesab.WPF.ViewModels.Shell;

public partial class MainViewModel : BaseViewModel
{
    private readonly IServiceProvider _services;
    private readonly IPersianCalendarService _calendar;
    private readonly ICurrentUserService _currentUser;
    private readonly ModuleService _modules;
    private readonly DispatcherTimer _clockTimer;

    // ── M3: پرچم‌های ماژول (برای نمایش/پنهان منو و سایدبار) ──
    public bool PosEnabled => _modules.IsEnabled(ModuleService.Pos);
    public bool RestaurantEnabled => _modules.IsEnabled(ModuleService.Restaurant);
    public bool TourismEnabled => _modules.IsEnabled(ModuleService.Tourism);
    public bool HrEnabled => _modules.IsEnabled(ModuleService.Hr);
    public bool CrmEnabled => _modules.IsEnabled(ModuleService.Crm);
    public bool HotelEnabled => _modules.IsEnabled(ModuleService.Hotel);

    // ── دسترسی منو بر اساس مجوز (RBAC). ADMIN/«*» همه را true می‌کند. ──
    public bool CanAccounting => _currentUser.HasPermission("Accounting", "Voucher", "View");
    public bool CanTreasury   => _currentUser.HasPermission("Treasury", "View", "");
    public bool CanSales      => _currentUser.HasPermission("Sales", "Invoice", "View");
    public bool CanPurchase   => _currentUser.HasPermission("Purchase", "Invoice", "View");
    public bool CanInventory  => _currentUser.HasPermission("Inventory", "View", "");
    public bool CanCustomers  => _currentUser.HasPermission("Customers", "View", "");
    public bool CanReports    => _currentUser.HasPermission("Reports", "View", "");
    public bool CanSecurity   => _currentUser.HasPermission("Security", "Manage", "");

    // نقشهٔ «کلید صفحه → ماژولِ اختیاری» (صفحات خارج از این نقشه = هسته، همیشه مجاز)
    private static readonly Dictionary<string, string> _pageModule = new()
    {
        ["POS"] = ModuleService.Pos, ["CashShift"] = ModuleService.Pos, ["PosDashboard"] = ModuleService.Pos,
        ["Employees"] = ModuleService.Hr, ["EmployeeEdit"] = ModuleService.Hr,
        ["Salary"] = ModuleService.Hr, ["Attendance"] = ModuleService.Hr,
    };

    [ObservableProperty] private BaseViewModel? _currentPage;
    [ObservableProperty] private WorkspaceTab? _selectedTab;
    public ObservableCollection<WorkspaceTab> OpenTabs { get; } = new();
    [ObservableProperty] private string _activeMenu = "Dashboard";
    [ObservableProperty] private string _quickSearch = string.Empty;
    [ObservableProperty] private int _notificationCount = 3;
    [ObservableProperty] private int _messageCount = 2;
    [ObservableProperty] private string _currentBranch = "شعبه مرکزی";
    [ObservableProperty] private string _currentPageTitle = "داشبورد";
    [ObservableProperty] private string _currentUserName = string.Empty;
    [ObservableProperty] private string _currentUserRole = string.Empty;
    [ObservableProperty] private string _companyName = "سماع رایانه کیش";
    [ObservableProperty] private string _todayPersianDate = string.Empty;
    [ObservableProperty] private string _statusMessage = "آماده";
    [ObservableProperty] private bool _isDarkTheme = true;

    private readonly Dictionary<string, (string Title, Func<IServiceProvider, BaseViewModel> Factory)> _pages;

    public MainViewModel(
        IServiceProvider services,
        IPersianCalendarService calendar,
        ICurrentUserService currentUser,
        IDialogService dialogService,
        INavigationService navigationService,
        ModuleService modules)
        : base(dialogService, navigationService)
    {
        _services = services;
        _calendar = calendar;
        _currentUser = currentUser;
        _modules = modules;
        _modules.Changed += () => System.Windows.Application.Current?.Dispatcher.Invoke(RaiseModuleFlags);

        _pages = new Dictionary<string, (string, Func<IServiceProvider, BaseViewModel>)>
        {
            ["Dashboard"]       = ("داشبورد",            sp => sp.GetRequiredService<DashboardViewModel>()),
            ["Vouchers"]        = ("اسناد حسابداری",      sp => sp.GetRequiredService<VoucherListViewModel>()),
            ["VoucherEdit"]     = ("ثبت سند",             sp => sp.GetRequiredService<VoucherEditViewModel>()),
            ["ChartOfAccounts"] = ("نمودار حساب‌ها",      sp => sp.GetRequiredService<ChartOfAccountsViewModel>()),
            ["Cheques"]         = ("مدیریت چک",           sp => sp.GetRequiredService<ChequeListViewModel>()),
            ["Receivables"]     = ("دریافتنی/پرداختنی",   sp => sp.GetRequiredService<SamaHesab.WPF.ViewModels.Treasury.ReceivablesViewModel>()),
            ["InterBranch"]     = ("تسویهٔ بین‌شعبه",      sp => sp.GetRequiredService<SamaHesab.WPF.ViewModels.Treasury.InterBranchTransferViewModel>()),
            ["ChequeBoard"]     = ("تابلوی چک",           sp => sp.GetRequiredService<ChequeBoardViewModel>()),
            ["EndOfPeriod"]     = ("عملیات پایان دوره",   sp => sp.GetRequiredService<EndOfPeriodViewModel>()),
            ["VoucherApprovals"]= ("کارتابلِ تأیید",       sp => sp.GetRequiredService<VoucherApprovalsViewModel>()),
            ["AccountantDash"]  = ("داشبورد حسابدار",      sp => sp.GetRequiredService<AccountantDashboardViewModel>()),
            ["ManagerDash"]     = ("داشبورد مدیریتی",      sp => sp.GetRequiredService<ManagerDashboardViewModel>()),
            ["AccDimensions"]   = ("ابعاد حسابداری",      sp => sp.GetRequiredService<AccountingDimensionsViewModel>()),
            ["Security"]        = ("امنیت و دسترسی",      sp => sp.GetRequiredService<SamaHesab.WPF.ViewModels.Security.SecurityManagementViewModel>()),
            ["AuditLog"]        = ("لاگِ حسابرسی",        sp => sp.GetRequiredService<SamaHesab.WPF.ViewModels.Security.AuditLogViewModel>()),
            ["Branches"]        = ("مدیریت شعب",          sp => sp.GetRequiredService<SamaHesab.WPF.ViewModels.Settings.BranchManagementViewModel>()),
            ["VoucherTools"]    = ("بهره‌وری سند",        sp => sp.GetRequiredService<VoucherProductivityViewModel>()),
            ["BankAccounts"]    = ("حساب‌های بانکی",      sp => sp.GetRequiredService<BankAccountViewModel>()),
            ["BankRecon"]       = ("مغایرت‌گیری بانکی",   sp => sp.GetRequiredService<BankReconciliationViewModel>()),
            ["Products"]        = ("مدیریت کالا",         sp => sp.GetRequiredService<ProductListViewModel>()),
            ["BatchSerial"]     = ("بچ و سریال",          sp => sp.GetRequiredService<BatchSerialViewModel>()),
            ["InventoryReport"] = ("گزارش انبار",          sp => sp.GetRequiredService<InventoryReportViewModel>()),
            ["ReorderReport"]   = ("گزارش نقطهٔ سفارش",    sp => sp.GetRequiredService<SamaHesab.WPF.ViewModels.Inventory.ReorderReportViewModel>()),
            ["PriceList"]       = ("مدیریت لیست‌قیمت",     sp => sp.GetRequiredService<SamaHesab.WPF.ViewModels.Inventory.PriceListViewModel>()),
            ["DiscountTiers"]   = ("تخفیف پلکانی",         sp => sp.GetRequiredService<SamaHesab.WPF.ViewModels.Inventory.DiscountTiersViewModel>()),
            ["SalesReport"]     = ("گزارش فروش",          sp => sp.GetRequiredService<SamaHesab.WPF.ViewModels.Sales.SalesReportViewModel>()),
            ["PurchaseReport"]  = ("گزارش خرید",          sp => sp.GetRequiredService<SamaHesab.WPF.ViewModels.Purchase.PurchaseReportViewModel>()),
            ["SupplierStatement"] = ("صورت‌حساب تأمین‌کننده", sp => sp.GetRequiredService<SamaHesab.WPF.ViewModels.Purchase.SupplierStatementViewModel>()),
            ["PurchaseOrders"]  = ("سفارش‌های خرید",      sp => sp.GetRequiredService<SamaHesab.WPF.ViewModels.Purchase.PurchaseOrderListViewModel>()),
            ["ProductEdit"]     = ("ویرایش کالا",         sp => sp.GetRequiredService<ProductEditViewModel>()),
            ["Warehouses"]      = ("انبارها",              sp => sp.GetRequiredService<WarehouseViewModel>()),
            ["StockAdjust"]     = ("تعدیل موجودی",        sp => sp.GetRequiredService<StockAdjustViewModel>()),
            ["StockTransfer"]   = ("انتقال بین انبار",     sp => sp.GetRequiredService<SamaHesab.WPF.ViewModels.Inventory.StockTransferViewModel>()),
            ["StockCount"]      = ("انبارگردانی",          sp => sp.GetRequiredService<SamaHesab.WPF.ViewModels.Inventory.StockCountViewModel>()),
            ["Kardex"]          = ("کاردکس کالا",          sp => sp.GetRequiredService<SamaHesab.WPF.ViewModels.Inventory.KardexViewModel>()),
            ["ProductCard"]     = ("کارت کالا",           sp => sp.GetRequiredService<SamaHesab.WPF.ViewModels.Inventory.ProductCardViewModel>()),
            ["SalesInvoice"]    = ("فاکتور فروش",         sp => sp.GetRequiredService<SalesInvoiceEditViewModel>()),
            ["SalesInvoiceList"]= ("لیست فروش",           sp => sp.GetRequiredService<SalesInvoiceListViewModel>()),
            ["RecurringInvoices"]= ("فاکتورهای تکرارشونده", sp => sp.GetRequiredService<SamaHesab.WPF.ViewModels.Sales.RecurringInvoiceListViewModel>()),
            ["PurchaseInvoice"] = ("فاکتور خرید",         sp => sp.GetRequiredService<PurchaseInvoiceEditViewModel>()),
            ["PurchaseInvoiceList"]= ("لیست خریدها",       sp => sp.GetRequiredService<PurchaseInvoiceListViewModel>()),
            ["POS"]             = ("صندوق فروش",          sp => sp.GetRequiredService<PosViewModel>()),
            ["PosDashboard"]    = ("داشبورد صندوق",       sp => sp.GetRequiredService<SamaHesab.WPF.ViewModels.POS.PosDashboardViewModel>()),
            ["CashShift"]       = ("صندوق / شیفت",        sp => sp.GetRequiredService<SamaHesab.WPF.ViewModels.POS.ShiftViewModel>()),
            ["Customers"]       = ("مشتریان",             sp => sp.GetRequiredService<CustomerListViewModel>()),
            ["CustomerEdit"]    = ("ویرایش مشتری",        sp => sp.GetRequiredService<CustomerEditViewModel>()),
            ["CustomerCard"]    = ("کارت مشتری",          sp => sp.GetRequiredService<SamaHesab.WPF.ViewModels.CRM.CustomerCardViewModel>()),
            ["Suppliers"]       = ("تأمین‌کنندگان",       sp => sp.GetRequiredService<SupplierListViewModel>()),
            ["Employees"]       = ("کارکنان",             sp => sp.GetRequiredService<EmployeeListViewModel>()),
            ["EmployeeEdit"]    = ("پرونده کارمند",       sp => sp.GetRequiredService<EmployeeEditViewModel>()),
            ["Salary"]          = ("حقوق و دستمزد",       sp => sp.GetRequiredService<SalaryViewModel>()),
            ["Attendance"]      = ("حضور و غیاب",         sp => sp.GetRequiredService<AttendanceViewModel>()),
            ["Reports"]         = ("گزارش‌ها",            sp => sp.GetRequiredService<ReportsViewModel>()),
            ["FinancialReports"]= ("گزارش‌های مالی",      sp => sp.GetRequiredService<SamaHesab.WPF.ViewModels.Reports.FinancialReportsViewModel>()),
            ["BranchReport"]    = ("گزارش تطبیقی شعب",    sp => sp.GetRequiredService<SamaHesab.WPF.ViewModels.Reports.BranchReportViewModel>()),
            ["Settings"]        = ("تنظیمات",             sp => sp.GetRequiredService<SettingsViewModel>()),
            ["Modules"]         = ("مدیریت ماژول‌ها",     sp => sp.GetRequiredService<ModulesViewModel>()),
            ["Backup"]          = ("پشتیبان‌گیری",         sp => sp.GetRequiredService<BackupViewModel>()),
        };

        // Clock timer
        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        _clockTimer.Tick += (_, _) => TodayPersianDate = _calendar.GetCurrentPersianDate();
        _clockTimer.Start();

        // Navigation service
        navigationService.Navigated += OnNavigationRequested;
    }

    public override async Task LoadAsync()
    {
        CurrentUserName = _currentUser.FullName ?? "کاربر";
        CurrentUserRole = string.Join(", ", _currentUser.GetRoles());
        TodayPersianDate = _calendar.GetCurrentPersianDate();
        RaiseAccessFlags();   // منوها بر اساس مجوزِ کاربرِ واردشده
        await NavigateToAsync("Dashboard");
    }

    private void OnNavigationRequested(object? sender, NavigationEventArgs e) =>
        System.Windows.Application.Current.Dispatcher.InvokeAsync(() => NavigateToAsync(e.ViewName, e.Parameter));

    [RelayCommand]
    private async Task NavigateAsync(string page) => await NavigateToAsync(page);

    private void RaiseAccessFlags()
    {
        OnPropertyChanged(nameof(CanAccounting)); OnPropertyChanged(nameof(CanTreasury));
        OnPropertyChanged(nameof(CanSales)); OnPropertyChanged(nameof(CanPurchase));
        OnPropertyChanged(nameof(CanInventory)); OnPropertyChanged(nameof(CanCustomers));
        OnPropertyChanged(nameof(CanReports)); OnPropertyChanged(nameof(CanSecurity));
    }

    private void RaiseModuleFlags()
    {
        OnPropertyChanged(nameof(PosEnabled)); OnPropertyChanged(nameof(RestaurantEnabled));
        OnPropertyChanged(nameof(TourismEnabled)); OnPropertyChanged(nameof(HrEnabled));
        OnPropertyChanged(nameof(CrmEnabled)); OnPropertyChanged(nameof(HotelEnabled));
    }

    private async Task NavigateToAsync(string page, object? parameter = null)
    {
        if (!_pages.TryGetValue(page, out var entry)) return;

        // M3: گیتِ ماژول — اگر صفحه به ماژولِ خاموش تعلق دارد، بازنشو
        if (_pageModule.TryGetValue(page, out var mod) && !_modules.IsEnabled(mod))
        {
            await _dialogService.ShowInfoAsync("این بخش به ماژولی تعلق دارد که غیرفعال است. از «تنظیمات → مدیریت ماژول‌ها» آن را فعال کنید.");
            return;
        }

        // Activate the tab if it is already open
        var existing = OpenTabs.FirstOrDefault(t => t.Key == page);
        if (existing != null)
        {
            SelectedTab = existing;
            ActiveMenu = page;
            CurrentPageTitle = entry.Title;
            if (parameter != null && existing.Content is Services.INavigationAware aware)
                await aware.OnNavigatedToAsync(parameter);
            return;
        }

        // Serialize page creation/loading so two quick clicks never run DB
        // queries concurrently on the shared DbContext.
        await _navLock.WaitAsync();
        try
        {
            // Each open screen gets its own DI scope → its own DbContext, so
            // multiple screens open at once never collide on a shared context.
            var scope = _services.CreateScope();
            var vm = entry.Factory(scope.ServiceProvider);
            var tab = new WorkspaceTab(page, entry.Title, vm, canClose: page != "Dashboard", scope);
            OpenTabs.Add(tab);
            SelectedTab = tab;
            CurrentPage = vm;
            ActiveMenu = page;
            CurrentPageTitle = entry.Title;
            await vm.LoadAsync();
            if (parameter != null && vm is Services.INavigationAware aware)
                await aware.OnNavigatedToAsync(parameter);
        }
        finally
        {
            _navLock.Release();
        }
    }

    private readonly System.Threading.SemaphoreSlim _navLock = new(1, 1);

    partial void OnSelectedTabChanged(WorkspaceTab? value)
    {
        if (value == null) return;
        CurrentPage = value.Content;
        ActiveMenu = value.Key;
        CurrentPageTitle = value.Title;
    }

    [RelayCommand]
    private void CloseTab(WorkspaceTab? tab)
    {
        if (tab == null || !tab.CanClose) return;
        var idx = OpenTabs.IndexOf(tab);
        OpenTabs.Remove(tab);
        if (SelectedTab == tab)
            SelectedTab = OpenTabs.Count > 0 ? OpenTabs[System.Math.Min(idx, OpenTabs.Count - 1)] : null;
        tab.Dispose();
    }

    [RelayCommand]
    private void ToggleTheme()
    {
        IsDarkTheme = !IsDarkTheme;
        var themePath = IsDarkTheme
            ? "Assets/Themes/Dark.xaml"
            : "Assets/Themes/Light.xaml";

        var app = System.Windows.Application.Current;
        var existing = app.Resources.MergedDictionaries
            .FirstOrDefault(d => d.Source?.OriginalString.Contains("Theme") == true
                              || d.Source?.OriginalString.Contains("Dark") == true
                              || d.Source?.OriginalString.Contains("Light") == true);

        if (existing != null) app.Resources.MergedDictionaries.Remove(existing);

        app.Resources.MergedDictionaries.Add(new System.Windows.ResourceDictionary
        {
            Source = new Uri(themePath, UriKind.Relative)
        });
    }

    [RelayCommand] private async Task UserProfileAsync() => await _dialogService.ShowInfoAsync($"کاربر: {CurrentUserName}");

    [RelayCommand]
    private void Logout()
    {
        var result = System.Windows.MessageBox.Show(
            "آیا می‌خواهید از سیستم خارج شوید؟", "خروج",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question);
        if (result != System.Windows.MessageBoxResult.Yes) return;

        _clockTimer.Stop();
        System.Windows.Application.Current.Shutdown();
    }

    [RelayCommand] private async Task ShowNotificationsAsync() => await _dialogService.ShowInfoAsync($"{NotificationCount} اعلان جدید دارید.");
    [RelayCommand] private async Task ShowMessagesAsync() => await _dialogService.ShowInfoAsync($"{MessageCount} پیام جدید دارید.");
    [RelayCommand] private async Task OpenCalculatorAsync()
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("calc.exe") { UseShellExecute = true }); }
        catch { await _dialogService.ShowInfoAsync("ماشین‌حساب در دسترس نیست."); }
    }
    [RelayCommand] private async Task ChangeBranchAsync() => await _dialogService.ShowInfoAsync("تغییر شعبه (در نسخه بعدی فعال می‌شود).");

    [RelayCommand]
    private void OpenPrintSettings()
    {
        var win = new SamaHesab.WPF.Views.Settings.PrintSettingsWindow
        {
            Owner = System.Windows.Application.Current.MainWindow
        };
        win.ShowDialog();
    }

    [RelayCommand]
    private void ChangeTheme(string theme)
    {
        SamaHesab.WPF.Services.ThemeManager.Apply(theme);
        SamaHesab.WPF.Services.AppSettingsStore.SaveTheme(theme);
        StatusMessage = $"پوسته به «{theme}» تغییر یافت.";
    }
}

public partial class WorkspaceTab : CommunityToolkit.Mvvm.ComponentModel.ObservableObject, IDisposable
{
    public string Key { get; }
    public string Title { get; }
    public bool CanClose { get; }
    public BaseViewModel Content { get; }
    private readonly IServiceScope? _scope;

    public WorkspaceTab(string key, string title, BaseViewModel content, bool canClose, IServiceScope? scope = null)
    {
        Key = key;
        Title = title;
        Content = content;
        CanClose = canClose;
        _scope = scope;
    }

    public void Dispose() => _scope?.Dispose();
}
