using SamaHesab.Domain.Entities.CRM;
using MediatR;
using SamaHesab.Application.Automation;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Enums;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.BI.Queries;

// ── DTOهای داشبورد (منبعِ واحد: API + دسکتاپ) ──
public record DashRecentInvoice(string Number, string Date, string Party, decimal Total, string Status);
public record DashChequeDue(string ChequeNumber, string BankName, decimal Amount, string DueDate, string Kind);
public record DashLowStock(string Code, string Name, decimal Qty, decimal Min);
public record DashPartyBalance(string Name, decimal Balance);
public record DashAlert(string Icon, string Text, string Level, string Nav);

public record DashboardDto(
    decimal TodaySales, decimal MonthSales, decimal TodayPurchase, decimal MonthPurchase,
    int TotalCustomers, int TotalProducts, int LowStockCount, int OverdueCheques,
    decimal Receivable, decimal Payable, decimal NetProfit, decimal TodayReceipt, decimal TodayPayment,
    List<DashRecentInvoice> RecentSales, List<DashRecentInvoice> RecentPurchases,
    List<DashChequeDue> ChequesDue, List<DashLowStock> LowStockItems,
    List<DashPartyBalance> TopCustomers, List<DashPartyBalance> Debtors, List<DashPartyBalance> Creditors,
    List<DashAlert> Alerts);

public record GetDashboardQuery(string Today) : IRequest<DashboardDto>;

public class GetDashboardQueryHandler : IRequestHandler<GetDashboardQuery, DashboardDto>
{
    private readonly IStockItemRepository _stock;
    private readonly IChequeRepository _cheques;
    private readonly IProductRepository _products;
    private readonly IRepository<Party> _customers;
    private readonly IRepository<Party> _suppliers;
    private readonly IRepository<SamaHesab.Domain.Entities.Sales.SalesInvoice> _sales;
    private readonly IRepository<SamaHesab.Domain.Entities.Purchase.PurchaseInvoice> _purchases;
    private readonly ICurrentUserService _currentUser;

    public GetDashboardQueryHandler(IStockItemRepository stock, IChequeRepository cheques,
        IProductRepository products,
        IRepository<Party> customers,
        IRepository<Party> suppliers,
        IRepository<SamaHesab.Domain.Entities.Sales.SalesInvoice> sales,
        IRepository<SamaHesab.Domain.Entities.Purchase.PurchaseInvoice> purchases,
        ICurrentUserService currentUser)
    { _stock = stock; _cheques = cheques; _products = products; _customers = customers;
      _suppliers = suppliers; _sales = sales; _purchases = purchases; _currentUser = currentUser; }

    private static string StatusFa(InvoiceStatus s) => s switch
    {
        InvoiceStatus.Draft => "پیش‌نویس", InvoiceStatus.Confirmed => "قطعی",
        InvoiceStatus.Posted => "قطعی", InvoiceStatus.Cancelled => "لغو شده", _ => s.ToString()
    };

    public async Task<DashboardDto> Handle(GetDashboardQuery req, CancellationToken ct)
    {
        var companyId = _currentUser.CompanyId ?? 1;
        var today = req.Today;
        var month = today.Length >= 7 ? today.Substring(0, 7) : today;

        var products  = await _products.FindAsync(p => p.CompanyId == companyId, ct);
        var customers = await _customers.FindAsync(c => c.CompanyId == companyId && c.IsCustomer, ct);
        var suppliers = await _suppliers.FindAsync(s => s.CompanyId == companyId && s.IsSupplier, ct);
        var sales     = await _sales.FindAsync(i => i.CompanyId == companyId, ct);
        var purchases = await _purchases.FindAsync(i => i.CompanyId == companyId, ct);
        var cheques   = await _cheques.FindAsync(c => c.CompanyId == companyId, ct);

        var custName = customers.ToDictionary(c => c.Id, c => c.FullName);
        var supName = suppliers.ToDictionary(s => s.Id, s => s.FullName);

        var recentSales = sales.OrderByDescending(x => x.Id).Take(8)
            .Select(i => new DashRecentInvoice(i.InvoiceNumber, i.InvoiceDate,
                custName.TryGetValue(i.CustomerId, out var n) ? n : $"#{i.CustomerId}", i.GrandTotal, StatusFa(i.Status))).ToList();

        var recentPurch = purchases.OrderByDescending(x => x.Id).Take(8)
            .Select(i => new DashRecentInvoice(i.InvoiceNumber, i.InvoiceDate,
                supName.TryGetValue(i.SupplierId, out var n) ? n : $"#{i.SupplierId}", i.GrandTotal, i.StatusCode)).ToList();

        var due = cheques.Where(c => c.Status == ChequeStatus.InProcess).OrderBy(c => c.DueDate).Take(10)
            .Select(c => new DashChequeDue(c.ChequeNumber, c.BankName, c.Amount, c.DueDate,
                c.ChequeType == ChequeType.Received ? "دریافتی" : "پرداختی")).ToList();
        var overdue = due.Count(c => string.CompareOrdinal(c.DueDate, today) < 0);

        var lowStock = new List<DashLowStock>();
        var stockInputs = new List<StockAlertInput>();
        foreach (var p in products)
        {
            var qty = await _stock.GetTotalQuantityAsync(p.Id, ct);
            if (p.MinStock > 0 && qty <= p.MinStock) lowStock.Add(new DashLowStock(p.Code, p.Name, qty, p.MinStock));
            if (p.MinStock > 0 || p.ReorderPoint > 0) stockInputs.Add(new StockAlertInput(p.Id, p.Name, qty, p.MinStock, p.ReorderPoint));
        }

        var topCustomers = customers.OrderByDescending(c => c.Balance).Take(8).Select(c => new DashPartyBalance(c.FullName, c.Balance)).ToList();
        var debtors = customers.Where(c => c.Balance > 0).OrderByDescending(c => c.Balance).Take(10).Select(c => new DashPartyBalance(c.FullName, c.Balance)).ToList();
        var creditors = suppliers.Where(s => s.Balance != 0).OrderByDescending(s => Math.Abs(s.Balance)).Take(10).Select(s => new DashPartyBalance(s.FullName, s.Balance)).ToList();

        var receivable = customers.Where(c => c.Balance > 0).Sum(c => c.Balance);
        var payable = suppliers.Where(s => s.Balance > 0).Sum(s => s.Balance);
        var monthSales = sales.Where(i => i.InvoiceDate.StartsWith(month)).Sum(i => i.GrandTotal);
        var monthPurch = purchases.Where(i => i.InvoiceDate.StartsWith(month)).Sum(i => i.GrandTotal);

        // ── اعلان‌ها: هم‌منطق با مرکزِ اعلان‌ها (AlertEngine)، سپس خلاصه‌سازی بر اساسِ دسته ──
        //   چک/موجودی/بدهی از همان موتور می‌آیند (انقضا این‌جا صرف‌نظر — batch بار نمی‌شود).
        var chequeInputs = cheques.Where(c => c.Status == ChequeStatus.InProcess)
            .Select(c => new ChequeAlertInput(c.Id, c.ChequeNumber, c.DueDate, c.Amount, c.ChequeType.ToString()));
        var debtInputs = sales.Where(i => i.RemainAmount > 0.01m && i.DueDate != null
                && i.Status != InvoiceStatus.Draft && i.Status != InvoiceStatus.Cancelled)
            .Select(i => new ReceivableAlertInput(i.Id, i.InvoiceNumber, i.DueDate, i.RemainAmount));
        var engineAlerts = AlertEngine.ChequeAlerts(chequeInputs, today)
            .Concat(AlertEngine.LowStockAlerts(stockInputs))
            .Concat(AlertEngine.DebtAlerts(debtInputs, today))
            .ToList();

        var alerts = new List<DashAlert>();
        void AddCat(string kind, string icon, string level, string nav, string label)
        {
            var n = engineAlerts.Count(a => a.Kind == kind);
            if (n > 0) alerts.Add(new DashAlert(icon, $"{n} {label}", level, nav));
        }
        AddCat("ChequeOverdue",      "🔴", "danger",  "ChequeBoard", "چک سررسیدگذشته");
        AddCat("ChequeDueToday",     "🟡", "warning", "ChequeBoard", "چک سررسیدِ امروز");
        AddCat("OutOfStock",         "🔴", "danger",  "Products",    "کالای ناموجود");
        AddCat("LowStock",           "⚠",  "warning", "Products",    "کالا زیرِ حداقلِ موجودی");
        AddCat("OverdueReceivable",  "🔴", "danger",  "Receivables", "فاکتورِ معوقِ دریافتنی");
        AddCat("ReceivableDueToday", "🟡", "warning", "Receivables", "فاکتورِ سررسیدِ امروز");

        return new DashboardDto(
            sales.Where(i => i.InvoiceDate == today).Sum(i => i.GrandTotal), monthSales,
            purchases.Where(i => i.InvoiceDate == today).Sum(i => i.GrandTotal), monthPurch,
            customers.Count, products.Count, lowStock.Count, overdue,
            receivable, payable, monthSales - monthPurch,
            sales.Where(i => i.InvoiceDate == today).Sum(i => i.PaidAmount),
            purchases.Where(i => i.InvoiceDate == today).Sum(i => i.PaidAmount),
            recentSales, recentPurch, due, lowStock, topCustomers, debtors, creditors, alerts);
    }
}
