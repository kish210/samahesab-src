using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Interfaces.Repositories;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.ObjectModel;

namespace SamaHesab.WPF.ViewModels.Dashboard;

public partial class DashboardViewModel : BaseViewModel
{
    private readonly ICurrentUserService _currentUser;
    private readonly IPersianCalendarService _calendar;
    private readonly IStockItemRepository _stockRepo;
    private readonly IChequeRepository _chequeRepo;
    private readonly IProductRepository _productRepo;
    private readonly IRepository<SamaHesab.Domain.Entities.CRM.Customer> _customerRepo;
    private readonly IRepository<SamaHesab.Domain.Entities.CRM.Supplier> _supplierRepo;

    // KPI Cards
    [ObservableProperty] private decimal _todaySales;
    [ObservableProperty] private decimal _monthSales;
    [ObservableProperty] private decimal _todayPurchase;
    [ObservableProperty] private decimal _monthPurchase;
    [ObservableProperty] private int _totalCustomers;
    [ObservableProperty] private int _totalProducts;
    [ObservableProperty] private int _lowStockCount;
    [ObservableProperty] private int _overdueCheques;
    [ObservableProperty] private decimal _cashBalance;
    [ObservableProperty] private decimal _receivable;
    [ObservableProperty] private decimal _payable;
    [ObservableProperty] private decimal _netProfit;
    [ObservableProperty] private decimal _todayReceipt;
    [ObservableProperty] private decimal _todayPayment;

    // Lists
    public ObservableCollection<DashboardAlert> Alerts { get; } = new();
    public ObservableCollection<RecentInvoice> RecentInvoices { get; } = new();
    public ObservableCollection<TopProduct> TopProducts { get; } = new();
    public ObservableCollection<ChequeDueItem> ChequesDue { get; } = new();
    public ObservableCollection<MonthlySalesPoint> SalesChart { get; } = new();
    public ObservableCollection<MonthlySalesPoint> WeeklySales { get; } = new();
    public ObservableCollection<MonthlySalesPoint> ProfitTrend { get; } = new();
    public ObservableCollection<DashboardTask> Tasks { get; } = new();
    public ObservableCollection<DashboardEvent> TodayEvents { get; } = new();

    public DashboardViewModel(ICurrentUserService currentUser, IPersianCalendarService calendar,
        IStockItemRepository stockRepo, IChequeRepository chequeRepo,
        IProductRepository productRepo,
        IRepository<SamaHesab.Domain.Entities.CRM.Customer> customerRepo,
        IRepository<SamaHesab.Domain.Entities.CRM.Supplier> supplierRepo,
        IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    {
        _currentUser = currentUser; _calendar = calendar;
        _stockRepo = stockRepo; _chequeRepo = chequeRepo;
        _productRepo = productRepo; _customerRepo = customerRepo; _supplierRepo = supplierRepo;
    }

    public override async Task LoadAsync()
    {
        await ExecuteAsync(async () =>
        {
            var companyId = _currentUser.CompanyId ?? 1;
            var today = _calendar.GetCurrentPersianDate();

            // ── Real figures from the database ──
            var products  = await _productRepo.FindAsync(p => p.CompanyId == companyId);
            var customers = await _customerRepo.FindAsync(c => c.CompanyId == companyId);
            var suppliers = await _supplierRepo.FindAsync(s => s.CompanyId == companyId);
            TotalProducts  = products.Count;
            TotalCustomers = customers.Count;
            LowStockCount  = products.Count(p => p.MinStock > 0); // approximate alert seed
            Receivable     = customers.Where(c => c.Balance > 0).Sum(c => c.Balance);
            Payable        = suppliers.Where(s => s.Balance < 0).Sum(s => -s.Balance);

            // KPIs that still need a fiscal/posting engine remain illustrative.
            TodaySales    = 12_500_000;
            MonthSales    = 385_000_000;
            TodayPurchase = 8_700_000;
            MonthPurchase = 145_000_000;
            OverdueCheques = 3;
            CashBalance    = 45_000_000;
            NetProfit      = MonthSales - MonthPurchase - 30_000_000;
            TodayReceipt   = 9_800_000;
            TodayPayment   = 5_300_000;

            // Alerts
            Alerts.Clear();
            if (LowStockCount > 0)
                Alerts.Add(new DashboardAlert("⚠", $"{LowStockCount} کالا به حداقل موجودی رسیده‌اند.", "warning", "Products"));
            if (OverdueCheques > 0)
                Alerts.Add(new DashboardAlert("🔴", $"{OverdueCheques} چک سررسید گذشته دارید.", "danger", "Cheques"));
            Alerts.Add(new DashboardAlert("ℹ", "پشتیبان‌گیری اخیر: دیروز ساعت ۲۳:۰۰", "info", "Backup"));

            // Recent Invoices
            RecentInvoices.Clear();
            RecentInvoices.Add(new RecentInvoice("F000234", today, "علی احمدی", 8_500_000, "قطعی"));
            RecentInvoices.Add(new RecentInvoice("F000233", today, "شرکت آلفا", 45_200_000, "قطعی"));
            RecentInvoices.Add(new RecentInvoice("F000232", today, "محمد رضایی", 2_100_000, "پیش‌نویس"));

            // Top Products
            TopProducts.Clear();
            TopProducts.Add(new TopProduct("روغن موتور ۵L", 320, 85_000_000));
            TopProducts.Add(new TopProduct("لاستیک ۱۷۵/۷۰R13", 180, 126_000_000));
            TopProducts.Add(new TopProduct("فیلتر هوا", 450, 22_500_000));

            // Sales Chart (last 6 months)
            SalesChart.Clear();
            var months = new[] { "مهر", "آبان", "آذر", "دی", "بهمن", "اسفند" };
            var vals   = new[] { 280m, 310m, 295m, 340m, 360m, 385m };
            for (int i = 0; i < months.Length; i++)
                SalesChart.Add(new MonthlySalesPoint(months[i], vals[i] * 1_000_000));

            // Cheques due
            ChequesDue.Clear();
            ChequesDue.Add(new ChequeDueItem("CH-1234", "بانک ملت", 15_000_000, today, "دریافتی"));
            ChequesDue.Add(new ChequeDueItem("CH-1235", "بانک صادرات", 8_000_000, today, "پرداختی"));

            // Weekly sales (last 7 days)
            WeeklySales.Clear();
            var days = new[] { "شنبه", "یکشنبه", "دوشنبه", "سه‌شنبه", "چهارشنبه", "پنجشنبه", "جمعه" };
            var dvals = new[] { 9m, 12m, 7m, 14m, 11m, 16m, 6m };
            for (int i = 0; i < days.Length; i++)
                WeeklySales.Add(new MonthlySalesPoint(days[i], dvals[i] * 1_000_000));

            // Profit trend
            ProfitTrend.Clear();
            var pvals = new[] { 40m, 52m, 48m, 61m, 70m, 85m };
            for (int i = 0; i < months.Length; i++)
                ProfitTrend.Add(new MonthlySalesPoint(months[i], pvals[i] * 1_000_000));

            // Tasks
            Tasks.Clear();
            Tasks.Add(new DashboardTask("پیگیری چک سررسید بانک ملت", "امروز", true));
            Tasks.Add(new DashboardTask("تماس با مشتری شرکت آلفا", "امروز", false));
            Tasks.Add(new DashboardTask("ثبت فاکتورهای خرید معوق", "فردا", false));
            Tasks.Add(new DashboardTask("بستن حساب‌های پایان ماه", "۳ روز دیگر", false));

            // Today's events
            TodayEvents.Clear();
            TodayEvents.Add(new DashboardEvent("۰۹:۰۰", "جلسه با تأمین‌کننده"));
            TodayEvents.Add(new DashboardEvent("۱۱:۳۰", "سررسید ۲ چک پرداختی"));
            TodayEvents.Add(new DashboardEvent("۱۴:۰۰", "بازرسی موجودی انبار مرکزی"));

            await Task.CompletedTask;
        }, "در حال بارگذاری داشبورد...");
    }

    [RelayCommand]
    private void NavigateTo(string page) => _navigationService.NavigateTo(page);

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync();
}

public record DashboardAlert(string Icon, string Message, string Type, string ActionPage);
public record RecentInvoice(string Number, string Date, string CustomerName, decimal Amount, string Status);
public record TopProduct(string Name, int QtySold, decimal Revenue);
public record ChequeDueItem(string ChequeNumber, string BankName, decimal Amount, string DueDate, string Type);
public record MonthlySalesPoint(string Month, decimal Amount);
public record DashboardTask(string Title, string Due, bool IsUrgent);
public record DashboardEvent(string Time, string Title);
