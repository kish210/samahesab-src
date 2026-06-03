using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Interfaces.Repositories;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Shell;
using System.Collections.ObjectModel;
using System.Linq;

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
    private readonly IRepository<SamaHesab.Domain.Entities.Sales.SalesInvoice> _salesRepo;
    private readonly IRepository<SamaHesab.Domain.Entities.Purchase.PurchaseInvoice> _purchaseRepo;

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
        IStockItemRepository stockRepo, IChequeRepository chequeRepo,
        IProductRepository productRepo,
        IRepository<SamaHesab.Domain.Entities.CRM.Customer> customerRepo,
        IRepository<SamaHesab.Domain.Entities.CRM.Supplier> supplierRepo,
        IRepository<SamaHesab.Domain.Entities.Sales.SalesInvoice> salesRepo,
        IRepository<SamaHesab.Domain.Entities.Purchase.PurchaseInvoice> purchaseRepo,
        IDialogService dialogService, INavigationService navigationService)
        : base(dialogService, navigationService)
    {
        _currentUser = currentUser; _calendar = calendar;
        _stockRepo = stockRepo; _chequeRepo = chequeRepo;
        _productRepo = productRepo; _customerRepo = customerRepo; _supplierRepo = supplierRepo;
        _salesRepo = salesRepo; _purchaseRepo = purchaseRepo;
    }

    public override async Task LoadAsync()
    {
        await ExecuteAsync(async () =>
        {
            var companyId = _currentUser.CompanyId ?? 1;
            var today = _calendar.GetCurrentPersianDate();           // 1403/06/15
            var month = today.Length >= 7 ? today.Substring(0, 7) : today; // 1403/06

            var products  = await _productRepo.FindAsync(p => p.CompanyId == companyId);
            var customers = await _customerRepo.FindAsync(c => c.CompanyId == companyId);
            var suppliers = await _supplierRepo.FindAsync(s => s.CompanyId == companyId);
            var sales     = await _salesRepo.FindAsync(i => i.CompanyId == companyId);
            var purchases = await _purchaseRepo.FindAsync(i => i.CompanyId == companyId);
            var cheques   = await _chequeRepo.FindAsync(c => c.CompanyId == companyId);

            TotalProducts  = products.Count;
            TotalCustomers = customers.Count;

            // ── KPI: sales / purchase by period (real persisted invoices) ──
            TodaySales    = sales.Where(i => i.InvoiceDate == today).Sum(i => i.GrandTotal);
            MonthSales    = sales.Where(i => i.InvoiceDate.StartsWith(month)).Sum(i => i.GrandTotal);
            TodayPurchase = purchases.Where(i => i.InvoiceDate == today).Sum(i => i.GrandTotal);
            MonthPurchase = purchases.Where(i => i.InvoiceDate.StartsWith(month)).Sum(i => i.GrandTotal);
            NetProfit     = MonthSales - MonthPurchase;

            Receivable = customers.Where(c => c.Balance > 0).Sum(c => c.Balance);
            Payable    = suppliers.Where(s => s.Balance > 0).Sum(s => s.Balance);
            TodayReceipt = sales.Where(i => i.InvoiceDate == today).Sum(i => i.PaidAmount);
            TodayPayment = purchases.Where(i => i.InvoiceDate == today).Sum(i => i.PaidAmount);

            // ── Recent sales (last 8) ──
            RecentInvoices.Clear();
            foreach (var i in sales.OrderByDescending(x => x.Id).Take(8))
            {
                customersName(customers, i.CustomerId, out var name);
                RecentInvoices.Add(new RecentInvoice(i.InvoiceNumber, i.InvoiceDate, name,
                    i.GrandTotal, StatusFa(i.Status)));
            }

            // ── Recent purchases (last 8) ──
            RecentPurchases.Clear();
            foreach (var i in purchases.OrderByDescending(x => x.Id).Take(8))
            {
                var sup = suppliers.FirstOrDefault(s => s.Id == i.SupplierId);
                RecentPurchases.Add(new RecentInvoice(i.InvoiceNumber, i.InvoiceDate,
                    sup?.FullName ?? $"#{i.SupplierId}", i.GrandTotal, i.StatusCode));
            }

            // ── Due cheques (in-process, soonest first) ──
            ChequesDue.Clear();
            foreach (var c in cheques
                         .Where(c => c.Status == SamaHesab.Domain.Enums.ChequeStatus.InProcess)
                         .OrderBy(c => c.DueDate).Take(10))
            {
                ChequesDue.Add(new ChequeDueItem(c.ChequeNumber, c.BankName, c.Amount, c.DueDate,
                    c.ChequeType == SamaHesab.Domain.Enums.ChequeType.Received ? "دریافتی" : "پرداختی"));
            }
            OverdueCheques = ChequesDue.Count(c => string.Compare(c.DueDate, today, System.StringComparison.Ordinal) < 0);

            // ── Low stock (real stock totals vs MinStock) ──
            LowStockItems.Clear();
            foreach (var p in products)
            {
                var qty = await _stockRepo.GetTotalQuantityAsync(p.Id);
                if (p.MinStock > 0 && qty <= p.MinStock)
                    LowStockItems.Add(new LowStockRow(p.Code, p.Name, qty, p.MinStock));
            }
            LowStockCount = LowStockItems.Count;

            // ── Top customers + outstanding debtors / creditors ──
            TopCustomers.Clear();
            foreach (var c in customers.OrderByDescending(c => c.Balance).Take(8))
                TopCustomers.Add(new PartyBalanceRow(c.FullName, c.Balance));

            Debtors.Clear();
            foreach (var c in customers.Where(c => c.Balance > 0).OrderByDescending(c => c.Balance).Take(10))
                Debtors.Add(new PartyBalanceRow(c.FullName, c.Balance));

            Creditors.Clear();
            foreach (var s in suppliers.Where(s => s.Balance != 0).OrderByDescending(s => System.Math.Abs(s.Balance)).Take(10))
                Creditors.Add(new PartyBalanceRow(s.FullName, s.Balance));

            // ── Alerts ──
            Alerts.Clear();
            if (LowStockCount > 0)
                Alerts.Add(new DashboardAlert("⚠", $"{LowStockCount} کالا به حداقل موجودی رسیده‌اند.", "warning", "Products"));
            if (OverdueCheques > 0)
                Alerts.Add(new DashboardAlert("🔴", $"{OverdueCheques} چک سررسید گذشته دارید.", "danger", "Cheques"));
            if (Receivable > 0)
                Alerts.Add(new DashboardAlert("📥", $"مانده دریافتنی از مشتریان: {Receivable:#,##0} ریال", "info", "Customers"));
        }, "در حال بارگذاری داشبورد...");
    }

    private static void customersName(System.Collections.Generic.List<SamaHesab.Domain.Entities.CRM.Customer> list,
        int id, out string name)
    {
        var c = list.FirstOrDefault(x => x.Id == id);
        name = c?.FullName ?? $"#{id}";
    }

    private static string StatusFa(SamaHesab.Domain.Enums.InvoiceStatus s) => s switch
    {
        SamaHesab.Domain.Enums.InvoiceStatus.Draft => "پیش‌نویس",
        SamaHesab.Domain.Enums.InvoiceStatus.Confirmed => "قطعی",
        SamaHesab.Domain.Enums.InvoiceStatus.Posted => "قطعی",
        SamaHesab.Domain.Enums.InvoiceStatus.Cancelled => "لغو شده",
        _ => s.ToString()
    };

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
