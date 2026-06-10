using System.Globalization;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.Inventory;
using SamaHesab.Domain.Entities.Sales;
using SamaHesab.Domain.Enums;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Automation.Queries;

/// <summary>
/// اعلان‌های عملیاتی لحظه‌ای برای داشبورد/زنگوله — فاز محصول‌سازی (P2).
/// منبع: چک سررسید + کسری موجودی + بدهی معوق (#۸) + انقضای موجودی (#۹).
/// </summary>
public record GetAlertsQuery(string Today) : IRequest<List<Alert>>;

public class GetAlertsQueryHandler : IRequestHandler<GetAlertsQuery, List<Alert>>
{
    private readonly IChequeRepository _cheques;
    private readonly IRepository<Product> _products;
    private readonly IRepository<StockItem> _stock;
    private readonly IRepository<SalesInvoice> _invoices;
    private readonly IRepository<Batch> _batches;
    private readonly ICurrentUserService _currentUser;

    public GetAlertsQueryHandler(IChequeRepository cheques, IRepository<Product> products,
        IRepository<StockItem> stock, IRepository<SalesInvoice> invoices,
        IRepository<Batch> batches, ICurrentUserService currentUser)
    {
        _cheques = cheques; _products = products; _stock = stock;
        _invoices = invoices; _batches = batches; _currentUser = currentUser;
    }

    public async Task<List<Alert>> Handle(GetAlertsQuery req, CancellationToken ct)
    {
        var companyId = _currentUser.CompanyId ?? 1;

        // ── چک‌های در جریان ──
        var cheques = (await _cheques.GetByStatusAsync(companyId, ChequeStatus.InProcess, ct))
            .Select(c => new ChequeAlertInput(c.Id, c.ChequeNumber, c.DueDate, c.Amount, c.ChequeType.ToString()));

        // ── کسری موجودی (کالاهای دارای آستانه) ──
        var products = await _products.FindAsync(
            p => p.CompanyId == companyId && (p.MinStock > 0 || p.ReorderPoint > 0), ct);
        var productIds = products.Select(p => p.Id).ToList();
        var onHand = (await _stock.FindAsync(s => productIds.Contains(s.ProductId), ct))
            .GroupBy(s => s.ProductId).ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));
        var stockInputs = products.Select(p => new StockAlertInput(
            p.Id, p.Name, onHand.TryGetValue(p.Id, out var q) ? q : 0, p.MinStock, p.ReorderPoint));

        // ── بدهی معوق (#۸): فاکتورهای فروشِ با ماندهٔ پرداخت ──
        var openInvoices = await _invoices.FindAsync(
            i => i.CompanyId == companyId && i.RemainAmount > 0.01m
                 && i.Status != InvoiceStatus.Draft && i.Status != InvoiceStatus.Cancelled
                 && i.DueDate != null, ct);
        var debtInputs = openInvoices.Select(i =>
            new ReceivableAlertInput(i.Id, i.InvoiceNumber, i.DueDate, i.RemainAmount));

        // ── انقضای موجودی (#۹): بچ‌های دارای موجودی، افق ۳۰ روز ──
        var horizon = AddDaysPersian(req.Today, 30);
        var allProducts = await _products.FindAsync(p => p.CompanyId == companyId, ct);
        var productNames = allProducts.ToDictionary(p => p.Id, p => p.Name);
        var allProductIds = allProducts.Select(p => p.Id).ToList();
        var batches = await _batches.FindAsync(
            b => allProductIds.Contains(b.ProductId) && b.Quantity > 0 && b.ExpiryDate != null, ct);
        var batchInputs = batches.Select(b => new BatchAlertInput(
            b.Id, productNames.TryGetValue(b.ProductId, out var n) ? n : $"#{b.ProductId}",
            b.BatchNumber, b.ExpiryDate, b.Quantity));

        var all = AlertEngine.ChequeAlerts(cheques, req.Today)
            .Concat(AlertEngine.LowStockAlerts(stockInputs))
            .Concat(AlertEngine.DebtAlerts(debtInputs, req.Today))
            .Concat(AlertEngine.ExpiryAlerts(batchInputs, req.Today, horizon));
        return AlertEngine.Sort(all);
    }

    /// <summary>افزودن n روز به تاریخ شمسی yyyy/MM/dd (با تبدیل امن میلادی↔شمسی).</summary>
    private static string AddDaysPersian(string persian, int days)
    {
        try
        {
            var p = persian.Split('/');
            var pc = new PersianCalendar();
            var dt = pc.ToDateTime(int.Parse(p[0]), int.Parse(p[1]), int.Parse(p[2]), 0, 0, 0, 0).AddDays(days);
            return $"{pc.GetYear(dt):0000}/{pc.GetMonth(dt):00}/{pc.GetDayOfMonth(dt):00}";
        }
        catch { return persian; }
    }
}
