using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Reports;
using SamaHesab.Application.Reports.Queries;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.ObjectModel;
using System.Linq;

namespace SamaHesab.WPF.ViewModels.Dashboard;

public partial class DashboardViewModel : BaseViewModel
{
    private readonly ICurrentUserService _currentUser;
    private readonly IPersianCalendarService _calendar;
    private readonly IMediator _mediator;
    private readonly ApiClient _api;

    // Workspace header (طبق ماک‌آپ طراح: سلام + تاریخ روز)
    [ObservableProperty] private string _greeting = "خوش آمدید";
    [ObservableProperty] private string _todayDateText = string.Empty;
    [ObservableProperty] private int _chequesDueCount;

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

    // Operational lists (all DB-backed)
    public ObservableCollection<DashboardAlert> Alerts { get; } = new();
    public ObservableCollection<RecentInvoice> RecentInvoices { get; } = new();   // recent sales
    public ObservableCollection<RecentInvoice> RecentPurchases { get; } = new();
    public ObservableCollection<TopProduct> TopProducts { get; } = new();
    public ObservableCollection<ChequeDueItem> ChequesDue { get; } = new();
    public ObservableCollection<LowStockRow> LowStockItems { get; } = new();
    public ObservableCollection<PartyBalanceRow> TopCustomers { get; } = new();
    public ObservableCollection<PartyBalanceRow> Debtors { get; } = new();
    public ObservableCollection<PartyBalanceRow> Creditors { get; } = new();

    public DashboardViewModel(ICurrentUserService currentUser, IPersianCalendarService calendar,
        IMediator mediator, ApiClient api,
        IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    {
        _currentUser = currentUser; _calendar = calendar;
        _mediator = mediator; _api = api;
    }

    public override async Task LoadAsync()
    {
        await ExecuteAsync(async () =>
        {
            var today = _calendar.GetCurrentPersianDate();           // 1403/06/15
            Greeting = $"خوش آمدید {_currentUser.FullName ?? "کاربر گرامی"}";
            TodayDateText = $"امروز — {today}";

            // 🏛️ کلاینت→API، دسکتاپ→Application — کلِ داشبورد در یک کوئریِ تجمیعی.
            var d = !string.IsNullOrWhiteSpace(_api.BaseUrl)
                ? await _api.GetDashboardAsync(today)
                : await _mediator.Send(new SamaHesab.Application.BI.Queries.GetDashboardQuery(today));
            if (d == null) return;

            TodaySales = d.TodaySales; MonthSales = d.MonthSales;
            TodayPurchase = d.TodayPurchase; MonthPurchase = d.MonthPurchase;
            TotalProducts = d.TotalProducts; TotalCustomers = d.TotalCustomers;
            NetProfit = d.NetProfit; Receivable = d.Receivable; Payable = d.Payable;
            TodayReceipt = d.TodayReceipt; TodayPayment = d.TodayPayment;
            LowStockCount = d.LowStockCount; OverdueCheques = d.OverdueCheques;

            RecentInvoices.Clear();
            foreach (var i in d.RecentSales) RecentInvoices.Add(new RecentInvoice(i.Number, i.Date, i.Party, i.Total, i.Status));
            RecentPurchases.Clear();
            foreach (var i in d.RecentPurchases) RecentPurchases.Add(new RecentInvoice(i.Number, i.Date, i.Party, i.Total, i.Status));
            ChequesDue.Clear();
            foreach (var c in d.ChequesDue) ChequesDue.Add(new ChequeDueItem(c.ChequeNumber, c.BankName, c.Amount, c.DueDate, c.Kind));
            ChequesDueCount = ChequesDue.Count;
            LowStockItems.Clear();
            foreach (var l in d.LowStockItems) LowStockItems.Add(new LowStockRow(l.Code, l.Name, l.Qty, l.Min));
            TopCustomers.Clear();
            foreach (var p in d.TopCustomers) TopCustomers.Add(new PartyBalanceRow(p.Name, p.Balance));
            Debtors.Clear();
            foreach (var p in d.Debtors) Debtors.Add(new PartyBalanceRow(p.Name, p.Balance));
            Creditors.Clear();
            foreach (var p in d.Creditors) Creditors.Add(new PartyBalanceRow(p.Name, p.Balance));
            Alerts.Clear();
            foreach (var a in d.Alerts) Alerts.Add(new DashboardAlert(a.Icon, a.Text, a.Level, a.Nav));

            // «کارهای امروزِ من» نقش‌محور (بک‌اندِ pc: GetDashboardAlertsQuery + DashboardRoleFilter).
            // فقط در حالتِ دسکتاپ (کوئریِ مستقیم)؛ نقشِ کاربر تعیین می‌کند کدام هشدارها دیده شوند
            // (مدیر همه). در صورتِ موفقیت، جایگزینِ فهرستِ بالا می‌شود؛ خطا → همان fallback می‌ماند.
            if (string.IsNullOrWhiteSpace(_api.BaseUrl))
            {
                try
                {
                    var raw = await _mediator.Send(new GetDashboardAlertsQuery(today));
                    var mine = DashboardRoleFilter.For(MapDashboardRole(), raw);
                    if (mine.Count > 0)
                    {
                        Alerts.Clear();
                        foreach (var a in mine) Alerts.Add(ToDashboardAlert(a));
                    }
                }
                catch { /* fallback: همان d.Alerts نمایش داده می‌شود */ }
            }
        }, "در حال بارگذاری داشبورد...");
    }

    /// <summary>نگاشتِ نقش‌های کاربر به نقشِ داشبورد (مدیر همه را می‌بیند؛ پیش‌فرض = مدیر تا چیزی پنهان نشود).</summary>
    private DashboardRole MapDashboardRole()
    {
        var roles = _currentUser.GetRoles().Select(r => r.ToLowerInvariant()).ToList();
        bool Has(params string[] keys) => roles.Any(r => keys.Any(r.Contains));
        if (Has("admin", "مدیر سیستم", "manager", "مدیر کل")) return DashboardRole.Manager;
        if (Has("خزانه", "treasur")) return DashboardRole.Treasurer;
        if (Has("حساب", "account")) return DashboardRole.Accountant;
        if (Has("انبار", "invent", "warehouse")) return DashboardRole.InventoryManager;
        if (Has("فروش", "sale")) return DashboardRole.Sales;
        if (Has("گردش", "tourism")) return DashboardRole.TourismOperator;
        if (Has("پروژه", "پیمان", "project")) return DashboardRole.ProjectManager;
        return DashboardRole.Manager;   // پیش‌فرضِ امن: همه را ببیند
    }

    private static DashboardAlert ToDashboardAlert(ActionableAlert a)
    {
        var icon = a.Key switch
        {
            "cheque-overdue" or "cheque-due-soon" => "💳",
            "receivable-overdue"                  => "📥",
            "stock-low"                           => "📦",
            "tourism-deposit-low"                 => "✈️",
            "guarantee-expiring"                  => "🛡️",
            _                                     => "⚠️"
        };
        var level = a.Severity switch
        {
            AlertSeverity.Critical => "critical",
            AlertSeverity.Warning  => "warning",
            _                      => "info"
        };
        var msg = a.Amount > 0 ? $"{a.Title} — {a.Amount:#,0} ریال" : a.Title;
        return new DashboardAlert(icon, msg, level, a.NavTarget);
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
public record LowStockRow(string Code, string Name, decimal Qty, decimal MinStock);
public record PartyBalanceRow(string Name, decimal Balance);
