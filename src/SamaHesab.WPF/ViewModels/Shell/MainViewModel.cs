using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Favorites;   // CC-3 — Recent/Pinned screens
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
    public bool SupportEnabled => _modules.IsEnabled(ModuleService.Support);   // 🆘 HC-1

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
        ["HelpCenter"] = ModuleService.Support, ["Diagnostics"] = ModuleService.Support,   // 🆘 HC-1
        ["BugReport"] = ModuleService.Support,   // 🆘 HC-3
        ["FeatureRequest"] = ModuleService.Support, ["SupportTicket"] = ModuleService.Support,
        ["MyRequests"] = ModuleService.Support,   // 🆘 HC-4
        ["ReleaseNotes"] = ModuleService.Support, ["KnowledgeBase"] = ModuleService.Support,   // 🆘 HC-5
        ["RemoteSupport"] = ModuleService.Support,   // 🆘 HC-6
    };

    [ObservableProperty] private BaseViewModel? _currentPage;
    [ObservableProperty] private WorkspaceTab? _selectedTab;
    public ObservableCollection<WorkspaceTab> OpenTabs { get; } = new();

    // CC-1 — جست‌وجوی سراسری: نتایج + وضعیتِ Popup + انتخاب (+ توکنِ debounce).
    public ObservableCollection<Services.Search.GlobalSearchResult> SearchResults { get; } = new();
    [ObservableProperty] private bool _isSearchOpen;
    [ObservableProperty] private Services.Search.GlobalSearchResult? _selectedSearchResult;
    private System.Threading.CancellationTokenSource? _searchCts;
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
    [ObservableProperty] private string _licenseBanner = string.Empty;   // فاز ۱۲ P-G7 — بنرِ وضعیتِ لایسنس

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

        // فاز ۱۲ P-G7 — بنرِ وضعیتِ لایسنس در نوارِ وضعیت.
        try
        {
            var lic = _services.GetService<LicenseService>();
            var s = lic?.GetStatus();
            LicenseBanner = s?.State switch
            {
                AppLicenseState.Activated   => $"✔ فعال — رده {s.License?.Tier}",
                AppLicenseState.Trial       => $"⏳ آزمایشی — {s.TrialDaysRemaining} روز و {s.TrialVouchersRemaining} سند",
                AppLicenseState.TrialExpired => "⛔ آزمایشی منقضی شد",
                AppLicenseState.Expired     => "⛔ لایسنس منقضی",
                AppLicenseState.Invalid     => "⛔ لایسنس نامعتبر",
                _ => string.Empty,
            };
        }
        catch { /* بنر نباید پوسته را بشکند */ }

        _pages = new Dictionary<string, (string, Func<IServiceProvider, BaseViewModel>)>
        {
            ["Dashboard"]       = ("داشبورد",            sp => sp.GetRequiredService<DashboardViewModel>()),
            ["Alerts"]          = ("مرکزِ اعلان‌ها",      sp => sp.GetRequiredService<SamaHesab.WPF.ViewModels.Automation.AlertsViewModel>()),   // کار #۲۵
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
            ["WarehouseDash"]   = ("داشبورد انبار",        sp => sp.GetRequiredService<SamaHesab.WPF.ViewModels.Inventory.WarehouseDashboardViewModel>()),
            ["WarehouseOverview"]=("نمای انبار",           sp => sp.GetRequiredService<SamaHesab.WPF.ViewModels.Inventory.WarehouseOverviewViewModel>()),
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
            ["SalesDash"]       = ("داشبورد فروش",        sp => sp.GetRequiredService<SamaHesab.WPF.ViewModels.Sales.SalesDashboardViewModel>()),
            ["RecurringInvoices"]= ("فاکتورهای تکرارشونده", sp => sp.GetRequiredService<SamaHesab.WPF.ViewModels.Sales.RecurringInvoiceListViewModel>()),
            ["Income"]          = ("درآمد و سود",         sp => sp.GetRequiredService<SamaHesab.WPF.ViewModels.Reports.IncomeReportViewModel>()),
            ["IncomeList"]      = ("لیست درآمدها",        sp => sp.GetRequiredService<SamaHesab.WPF.ViewModels.Reports.IncomeReportViewModel>()),
            ["PurchaseInvoice"] = ("فاکتور خرید",         sp => sp.GetRequiredService<PurchaseInvoiceEditViewModel>()),
            ["PurchaseInvoiceList"]= ("لیست خریدها",       sp => sp.GetRequiredService<PurchaseInvoiceListViewModel>()),
            ["POS"]             = ("صندوق فروش",          sp => sp.GetRequiredService<PosViewModel>()),
            ["PosDashboard"]    = ("داشبورد صندوق",       sp => sp.GetRequiredService<SamaHesab.WPF.ViewModels.POS.PosDashboardViewModel>()),
            ["CashShift"]       = ("صندوق / شیفت",        sp => sp.GetRequiredService<SamaHesab.WPF.ViewModels.POS.ShiftViewModel>()),
            ["Persons"]         = ("اشخاص",              sp => sp.GetRequiredService<SamaHesab.WPF.ViewModels.CRM.PersonsListViewModel>()),
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
            ["AgedBalance"]     = ("ماندهٔ سنی‌شده",       sp => sp.GetRequiredService<SamaHesab.WPF.ViewModels.Reports.AgedBalanceViewModel>()),
            ["VatSummary"]      = ("خلاصهٔ مالیات ارزش‌افزوده", sp => sp.GetRequiredService<SamaHesab.WPF.ViewModels.Reports.VatSummaryViewModel>()),
            ["Daybook"]         = ("دفترِ روزنامه",       sp => sp.GetRequiredService<SamaHesab.WPF.ViewModels.Reports.DaybookViewModel>()),
            ["DeadStock"]       = ("کالای راکد",          sp => sp.GetRequiredService<SamaHesab.WPF.ViewModels.Reports.DeadStockViewModel>()),
            ["ProductProfit"]   = ("سود و زیانِ کالا",     sp => sp.GetRequiredService<SamaHesab.WPF.ViewModels.Reports.ProductProfitViewModel>()),
            ["AbcAnalysis"]     = ("تحلیلِ ABC کالا",      sp => sp.GetRequiredService<SamaHesab.WPF.ViewModels.Reports.AbcAnalysisViewModel>()),
            ["Turnover"]        = ("گردشِ موجودی",        sp => sp.GetRequiredService<SamaHesab.WPF.ViewModels.Reports.InventoryTurnoverViewModel>()),
            ["Settings"]        = ("تنظیمات",             sp => sp.GetRequiredService<SettingsViewModel>()),
            ["Modules"]         = ("مدیریت ماژول‌ها",     sp => sp.GetRequiredService<ModulesViewModel>()),
            ["Backup"]          = ("پشتیبان‌گیری",         sp => sp.GetRequiredService<BackupViewModel>()),
            ["DocumentTemplates"]= ("قالبِ اسناد",         sp => sp.GetRequiredService<SamaHesab.WPF.ViewModels.Settings.DocumentTemplatesViewModel>()),
            ["DataImport"]      = ("ورودِ داده از اکسل",   sp => sp.GetRequiredService<SamaHesab.WPF.ViewModels.Settings.DataImportViewModel>()),
            // 🆘 HC-1 — مرکزِ پشتیبانی (ماژولِ اختیاریِ Support)
            ["HelpCenter"]      = ("مرکزِ پشتیبانی",       sp => sp.GetRequiredService<SamaHesab.WPF.ViewModels.Support.HelpCenterViewModel>()),
            ["Diagnostics"]     = ("عیب‌یابیِ سیستم",      sp => sp.GetRequiredService<SamaHesab.WPF.ViewModels.Support.DiagnosticsViewModel>()),
            ["BugReport"]       = ("گزارشِ باگ",          sp => sp.GetRequiredService<SamaHesab.WPF.ViewModels.Support.BugReportViewModel>()),
            ["FeatureRequest"]  = ("درخواستِ قابلیت",     sp => sp.GetRequiredService<SamaHesab.WPF.ViewModels.Support.FeatureRequestViewModel>()),
            ["SupportTicket"]   = ("تیکتِ پشتیبانی",      sp => sp.GetRequiredService<SamaHesab.WPF.ViewModels.Support.SupportTicketViewModel>()),
            ["MyRequests"]      = ("درخواست‌های من",      sp => sp.GetRequiredService<SamaHesab.WPF.ViewModels.Support.MyRequestsViewModel>()),
            ["ReleaseNotes"]    = ("یادداشت‌های نسخه",    sp => sp.GetRequiredService<SamaHesab.WPF.ViewModels.Support.ReleaseNotesViewModel>()),
            ["KnowledgeBase"]   = ("دانشنامه",           sp => sp.GetRequiredService<SamaHesab.WPF.ViewModels.Support.KnowledgeBaseViewModel>()),
            ["RemoteSupport"]   = ("پشتیبانیِ ریموت",     sp => sp.GetRequiredService<SamaHesab.WPF.ViewModels.Support.RemoteSupportViewModel>()),
        };

        // Clock timer
        _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
        _clockTimer.Tick += (_, _) => TodayPersianDate = _calendar.GetCurrentPersianDate();
        _clockTimer.Start();

        // Navigation service
        navigationService.Navigated += OnNavigationRequested;

        BuildNav();   // سایدبارِ آکاردئونی (بر اساسِ مجوز/ماژول)
    }

    public override async Task LoadAsync()
    {
        CurrentUserName = _currentUser.FullName ?? "کاربر";
        CurrentUserRole = string.Join(", ", _currentUser.GetRoles());
        TodayPersianDate = _calendar.GetCurrentPersianDate();
        RaiseAccessFlags();   // منوها بر اساس مجوزِ کاربرِ واردشده
        await NavigateToAsync(PickRoleDashboard());   // کار #۹ — ورودِ اولیه به داشبوردِ متناسب با نقش
        await RefreshNotificationCountAsync();         // کار #۲۵ — شمارندهٔ واقعیِ زنگوله
    }

    /// <summary>کار #۲۵ — تعدادِ اعلان‌های فعال برای بَجِ زنگوله (best-effort؛ نباید پوسته را بشکند).</summary>
    private async Task RefreshNotificationCountAsync()
    {
        try
        {
            var mediator = _services.GetService<MediatR.IMediator>();
            if (mediator is null) return;
            var alerts = await mediator.Send(
                new SamaHesab.Application.Automation.Queries.GetAlertsQuery(_calendar.GetCurrentPersianDate()));
            NotificationCount = alerts.Count;
        }
        catch { /* بَج نباید پوسته را بشکند */ }
    }

    /// <summary>
    /// کار #۹ — داشبوردِ متناسب با نقشِ کاربر برای ورودِ اولیه (با fallback به داشبوردِ عمومیِ جامع).
    /// منوی «داشبورد» همچنان عمومی است؛ این فقط صفحهٔ آغازین را هوشمند می‌کند.
    /// </summary>
    private string PickRoleDashboard()
    {
        var roles = _currentUser.GetRoles().Select(r => r.ToUpperInvariant()).ToHashSet();
        if (roles.Contains("ACCOUNTANT")) return "AccountantDash";
        if (roles.Contains("WAREHOUSE")) return "WarehouseDash";
        if (roles.Contains("CASHIER") && _modules.IsEnabled(ModuleService.Pos)) return "PosDashboard";
        if (roles.Contains("SALES")) return "SalesDash";
        if (roles.Contains("MANAGER")) return "ManagerDash";
        return "Dashboard";   // ادمین/خرید/HR/مشاهده‌کننده/ناشناخته → داشبوردِ عمومی
    }

    private void OnNavigationRequested(object? sender, NavigationEventArgs e) =>
        System.Windows.Application.Current.Dispatcher.InvokeAsync(() => NavigateToAsync(e.ViewName, e.Parameter));

    [RelayCommand]
    private async Task NavigateAsync(string page) => await NavigateToAsync(page);

    // ── CC-1 — جست‌وجوی سراسری: debounce روی تایپ → Popupِ نتایج؛ Enter/کلیک → بازکردن در Tab ──
    partial void OnQuickSearchChanged(string value)
    {
        _searchCts?.Cancel();
        var term = (value ?? string.Empty).Trim();
        if (term.Length < 2) { SearchResults.Clear(); IsSearchOpen = false; return; }
        var cts = new System.Threading.CancellationTokenSource();
        _searchCts = cts;
        _ = RunSearchAsync(term, cts.Token);
    }

    private async Task RunSearchAsync(string term, System.Threading.CancellationToken ct)
    {
        try
        {
            await Task.Delay(250, ct);   // debounce — تا با هر کلید کوئری نزنیم
            // دستورهای ناوبری (همیشه در دسترس، حتی روی DBِ خالی) + نتایجِ دادهٔ زنده.
            var cmds = BuildPaletteCommands()
                .Where(c => c.Title.Contains(term, StringComparison.OrdinalIgnoreCase))
                .Take(8).ToList();
            var svc = _services.GetRequiredService<Services.Search.IGlobalSearchService>();
            var res = await svc.SearchAsync(term, perGroupCap: 6, ct);
            if (ct.IsCancellationRequested) return;
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                SearchResults.Clear();
                foreach (var r in cmds.Concat(res).Take(40)) SearchResults.Add(r);
                SelectedSearchResult = SearchResults.Count > 0 ? SearchResults[0] : null;
                IsSearchOpen = SearchResults.Count > 0;
            });
        }
        catch (TaskCanceledException) { }
        catch { /* جست‌وجو نباید پوسته را بشکند */ }
    }

    // ── CC-2 — Command Palette: دستورهای ناوبری (همهٔ صفحات) برای ادغام با نتایجِ CC-1 ──
    /// <summary>فهرستِ دستورهای ناوبری از نقشهٔ صفحات (گروه «برو به») — paletteِ Ctrl+K از این می‌سازد.</summary>
    public IReadOnlyList<Services.Search.GlobalSearchResult> BuildPaletteCommands()
        => _pages.Select(kv => new Services.Search.GlobalSearchResult(
                "برو به", "→", kv.Value.Title, string.Empty, kv.Key))
            .OrderBy(r => r.Title)
            .ToList();

    /// <summary>سرویسِ جست‌وجوی سراسری برای ادغامِ نتایجِ دادهٔ زنده در palette.</summary>
    public Services.Search.IGlobalSearchService SearchService
        => _services.GetRequiredService<Services.Search.IGlobalSearchService>();

    [RelayCommand]
    private async Task OpenSearchResultAsync(Services.Search.GlobalSearchResult? r)
    {
        r ??= SelectedSearchResult;
        if (r == null) return;
        IsSearchOpen = false;
        QuickSearch = string.Empty;
        SearchResults.Clear();
        await NavigateToAsync(r.NavKey, r.Param);
    }

    // ── CC-3 — دسترسیِ سریع: صفحاتِ پین‌شده (★) + اخیر (☆) در سایدبار ──
    public ObservableCollection<QuickAccessItem> QuickAccess { get; } = new();
    public bool HasQuickAccess => QuickAccess.Count > 0;

    /// <summary>هشِ پایدارِ کلیدِ صفحه (FNV-1a) — EntityIdِ یکتا برای UserItemRef.</summary>
    private static int ScreenId(string key)
    {
        unchecked { uint h = 2166136261; foreach (var c in key) { h ^= c; h *= 16777619; } return (int)(h & 0x7FFFFFFF); }
    }

    private async Task TouchScreenAsync(string key)
    {
        try
        {
            if (!_pages.ContainsKey(key)) return;
            using var scope = _services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<MediatR.IMediator>()
                .Send(new TouchRecentItemCommand("Screen", ScreenId(key), key));
            await LoadQuickAccessAsync();
        }
        catch { /* ردیابیِ اخیر نباید ناوبری را بشکند */ }
    }

    /// <summary>بازخوانیِ پین‌شده‌ها + اخیرها برای سایدبار (پین‌شده اول، بدونِ تکرار، فقط کلیدهای معتبر).</summary>
    public async Task LoadQuickAccessAsync()
    {
        try
        {
            List<ItemRefDto> pinned, recent;
            using (var scope = _services.CreateScope())
            {
                var med = scope.ServiceProvider.GetRequiredService<MediatR.IMediator>();
                pinned = await med.Send(new GetPinnedItemsQuery("Screen"));
                recent = await med.Send(new GetRecentItemsQuery("Screen", 6));
            }
            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                QuickAccess.Clear();
                var seen = new HashSet<string>();
                foreach (var p in pinned)
                    if (_pages.TryGetValue(p.Label, out var e) && seen.Add(p.Label))
                        QuickAccess.Add(new QuickAccessItem(p.Label, e.Title, true));
                foreach (var r in recent)
                    if (_pages.TryGetValue(r.Label, out var e) && seen.Add(r.Label))
                        QuickAccess.Add(new QuickAccessItem(r.Label, e.Title, false));
                OnPropertyChanged(nameof(HasQuickAccess));
            });
        }
        catch { /* سایدبار نباید پوسته را بشکند */ }
    }

    [RelayCommand]
    private async Task TogglePinScreenAsync(QuickAccessItem? item)
    {
        if (item is null) return;
        try
        {
            using (var scope = _services.CreateScope())
                await scope.ServiceProvider.GetRequiredService<MediatR.IMediator>()
                    .Send(new SetPinnedItemCommand("Screen", ScreenId(item.Key), item.Key, !item.Pinned));
            await LoadQuickAccessAsync();
        }
        catch { }
    }

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
        OnPropertyChanged(nameof(SupportEnabled));
        BuildNav();   // بازسازیِ سایدبارِ آکاردئونی با تغییرِ ماژول‌ها
    }

    // ── CC-3+ — سایدبارِ آکاردئونی (داده‌محور، با رعایتِ RBAC/ماژول؛ منعکسِ منوی بالا) ──
    public ObservableCollection<NavGroup> NavGroups { get; } = new();

    [RelayCommand]
    private void ToggleGroup(NavGroup? g)
    {
        if (g is null) return;
        bool wasOpen = g.IsExpanded;
        foreach (var x in NavGroups) x.IsExpanded = false;   // آکاردئون: یک بخشِ باز
        g.IsExpanded = !wasOpen;
    }

    private void BuildNav()
    {
        NavGroups.Clear();
        void Add(bool show, string title, string icon, params NavLink[] links)
        { if (show && links.Length > 0) NavGroups.Add(new NavGroup { Title = title, IconKey = icon, Links = links }); }

        Add(CanAccounting, "حسابداری", "IcAccounting",
            new("ثبت سند", "VoucherEdit"), new("اسناد حسابداری", "Vouchers"), new("نمودار حساب‌ها", "ChartOfAccounts"),
            new("ابعاد حسابداری", "AccDimensions"), new("بهره‌وری سند", "VoucherTools"), new("عملیات پایان دوره", "EndOfPeriod"),
            new("کارتابلِ تأیید", "VoucherApprovals"), new("داشبورد حسابدار", "AccountantDash"), new("داشبورد مدیریتی", "ManagerDash"));

        Add(CanTreasury, "خزانه‌داری", "IcTreasury",
            new("مدیریت چک", "Cheques"), new("تابلوی چک", "ChequeBoard"), new("دریافتنی/پرداختنی", "Receivables"),
            new("تسویهٔ بین‌شعبه", "InterBranch"), new("حساب‌های بانکی", "BankAccounts"), new("مغایرت‌گیری بانکی", "BankRecon"));

        Add(CanSales, "فروش و درآمد", "IcSales",
            new("داشبورد فروش", "SalesDash"), new("فروش جدید", "SalesInvoice"), new("فاکتورهای فروش", "SalesInvoiceList"),
            new("فاکتورهای تکرارشونده", "RecurringInvoices"), new("درآمد و سود", "Income"), new("لیست درآمدها", "IncomeList"),
            new("گزارش فروش", "SalesReport"));

        Add(CanPurchase, "خرید", "IcPurchasing",
            new("فاکتور خرید", "PurchaseInvoice"), new("لیست خریدها", "PurchaseInvoiceList"), new("سفارش‌های خرید", "PurchaseOrders"),
            new("گزارش خرید", "PurchaseReport"), new("صورت‌حساب تأمین‌کننده", "SupplierStatement"));

        Add(CanInventory, "انبار", "IcInventory",
            new("کالاها", "Products"), new("مدیریت لیست‌قیمت", "PriceList"), new("تخفیف پلکانی", "DiscountTiers"),
            new("بچ و سریال (انقضا)", "BatchSerial"), new("گزارش موجودی/ارزش انبار", "InventoryReport"), new("گزارش نقطهٔ سفارش", "ReorderReport"),
            new("نمای انبار", "WarehouseOverview"), new("انبارها", "Warehouses"), new("تعدیل موجودی", "StockAdjust"),
            new("انبارگردانی", "StockCount"), new("انتقال بین انبار", "StockTransfer"), new("کاردکس کالا", "Kardex"));

        Add(CanCustomers, "اشخاص", "IcPeople",
            new("همهٔ اشخاص", "Persons"), new("مشتریان", "Customers"), new("تأمین‌کنندگان", "Suppliers"));

        Add(CanReports, "گزارشات", "IcReports",
            new("گزارش‌ها", "Reports"), new("گزارش‌های مالی", "FinancialReports"), new("گزارش تطبیقی شعب", "BranchReport"),
            new("ماندهٔ سنی‌شده", "AgedBalance"), new("خلاصهٔ مالیات ارزش‌افزوده", "VatSummary"), new("دفترِ روزنامه", "Daybook"),
            new("کالای راکد", "DeadStock"), new("سود و زیانِ کالا", "ProductProfit"), new("تحلیلِ ABC", "AbcAnalysis"), new("گردشِ موجودی", "Turnover"));

        // سیستم — همیشه (با گیتِ داخلیِ هر آیتم)
        var sys = new List<NavLink> { new("میز کار / داشبورد", "Dashboard") };
        if (CanSecurity) { sys.Add(new("امنیت و دسترسی", "Security")); sys.Add(new("لاگِ حسابرسی", "AuditLog")); sys.Add(new("مدیریت شعب", "Branches")); }
        if (HrEnabled)  { sys.Add(new("کارکنان", "Employees")); sys.Add(new("حقوق و دستمزد", "Salary")); }
        sys.Add(new("مدیریت ماژول‌ها", "Modules")); sys.Add(new("قالبِ اسناد", "DocumentTemplates"));
        sys.Add(new("ورودِ داده از اکسل", "DataImport")); sys.Add(new("تنظیمات", "Settings")); sys.Add(new("پشتیبان‌گیری", "Backup"));
        NavGroups.Add(new NavGroup { Title = "سیستم", IconKey = "IcSettings", Links = sys });

        if (TourismEnabled)
            Add(true, "گردشگری", "IcSales",
                new("رزروها", "TourismBookings"), new("تورها", "TourismTours"), new("هتل‌ها", "TourismHotels"),
                new("پروازها", "TourismFlights"), new("ترانسفر", "TourismTransfer"), new("واچرها", "TourismVouchers"),
                new("کمیسیون‌ها", "TourismCommissions"), new("گزارشات گردشگری", "TourismReports"));

        if (SupportEnabled)
            Add(true, "مرکزِ پشتیبانی", "IcReports",
                new("داشبورد", "HelpCenter"), new("گزارشِ باگ", "BugReport"), new("درخواستِ قابلیت", "FeatureRequest"),
                new("تیکتِ پشتیبانی", "SupportTicket"), new("درخواست‌های من", "MyRequests"), new("دانشنامه", "KnowledgeBase"),
                new("یادداشت‌های نسخه", "ReleaseNotes"), new("پشتیبانیِ ریموت", "RemoteSupport"), new("عیب‌یابیِ سیستم", "Diagnostics"));
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

        // CC-3 — ثبتِ صفحه برای «اخیر/پین‌شده» (fire-and-forget، خارج از قفلِ ناوبری).
        _ = TouchScreenAsync(page);

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

    // کار #۲۵ — زنگولهٔ اعلان → بازکردنِ مرکزِ اعلان‌ها.
    [RelayCommand] private async Task ShowNotificationsAsync() => await NavigateToAsync("Alerts");
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

/// <summary>CC-3 — یک آیتمِ «دسترسیِ سریع» در سایدبار (صفحهٔ پین‌شده یا اخیر).</summary>
public record QuickAccessItem(string Key, string Title, bool Pinned)
{
    public string PinGlyph => Pinned ? "★" : "☆";
}

/// <summary>یک پیوندِ زیرمنوی سایدبار (عنوان + کلیدِ صفحه).</summary>
public record NavLink(string Title, string Key);

/// <summary>یک بخشِ آکاردئونیِ سایدبار (عنوان + آیکون + زیرمنوها + وضعیتِ باز/بسته).</summary>
public partial class NavGroup : CommunityToolkit.Mvvm.ComponentModel.ObservableObject
{
    public string Title { get; init; } = "";
    public string IconKey { get; init; } = "";
    public IReadOnlyList<NavLink> Links { get; init; } = System.Array.Empty<NavLink>();
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty] private bool _isExpanded;
}
