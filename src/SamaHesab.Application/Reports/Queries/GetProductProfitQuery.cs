using MediatR;
using SamaHesab.Application.BI.Queries;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.Inventory;
using SamaHesab.Domain.Entities.Sales;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Reports.Queries;

/// <summary>یک ردیفِ گزارشِ سود و زیانِ کالا.</summary>
public record ProductProfitRow(string Code, string Name, decimal Quantity,
    decimal Revenue, decimal Cost, decimal Profit, decimal MarginPercent);

public record ProductProfitResult(IReadOnlyList<ProductProfitRow> Rows,
    decimal TotalRevenue, decimal TotalCost, decimal TotalProfit, decimal MarginPercent);

/// <summary>
/// فاز ۱۲ (پولیش) — سود و زیانِ کالا/فروش در یک بازه. درآمد از اقلامِ فاکتورِ فروشِ قطعی،
/// و **بهای تمام‌شده (COGS) از خروجی‌های انبار (`StockTransaction`)** که هنگامِ فروش با بهای
/// واقعیِ خروج ثبت شده‌اند — مستقل از فیلدِ `SalesInvoiceItem.Profit` (که در ثبتِ فاکتور پر نمی‌شود).
/// </summary>
public record GetProductProfitQuery(string FromDate, string ToDate) : IRequest<ProductProfitResult>;

public class GetProductProfitQueryHandler : IRequestHandler<GetProductProfitQuery, ProductProfitResult>
{
    private readonly IRepository<SalesInvoice> _invoices;
    private readonly IRepository<SalesInvoiceItem> _items;
    private readonly IRepository<StockTransaction> _ledger;
    private readonly IRepository<Product> _products;
    private readonly ICurrentUserService _user;

    public GetProductProfitQueryHandler(IRepository<SalesInvoice> invoices, IRepository<SalesInvoiceItem> items,
        IRepository<StockTransaction> ledger, IRepository<Product> products, ICurrentUserService user)
    { _invoices = invoices; _items = items; _ledger = ledger; _products = products; _user = user; }

    public async Task<ProductProfitResult> Handle(GetProductProfitQuery req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        var from = req.FromDate; var to = req.ToDate;

        // درآمد: اقلامِ فاکتورهای فروشِ قطعیِ بازه
        var sales = await SalesQueryHelper.LoadSalesAsync(_invoices, companyId, from, to, ct);
        var ids = sales.Select(s => s.Id).ToHashSet();
        var items = (await _items.FindAsync(it => ids.Contains(it.InvoiceId), ct)).ToList();

        // COGS: خروجی‌های انبارِ مربوط به فروش در همان بازه (بهای واقعیِ خروج)
        var outflows = await _ledger.FindAsync(
            t => t.CompanyId == companyId && t.Quantity < 0 && t.RelatedDocType == "SalesInvoice"
                 && string.Compare(t.DocumentDate, from) >= 0 && string.Compare(t.DocumentDate, to) <= 0, ct);
        var cogsByProduct = outflows.GroupBy(t => t.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(t => -t.TotalCost));   // TotalCost = Quantity(منفی)×UnitCost

        var names = (await _products.FindAsync(p => p.CompanyId == companyId, ct))
            .ToDictionary(p => p.Id, p => (p.Code, p.Name));

        var rows = items.GroupBy(it => it.ProductId).Select(g =>
        {
            var revenue = g.Sum(x => x.NetAmount);
            var qty = g.Sum(x => x.Quantity);
            var cost = cogsByProduct.TryGetValue(g.Key, out var c) ? c : 0m;
            var profit = revenue - cost;
            var (code, name) = names.TryGetValue(g.Key, out var p) ? p : ($"#{g.Key}", "");
            var margin = revenue != 0 ? Math.Round(profit / revenue * 100, 1) : 0m;
            return new ProductProfitRow(code, name, qty, revenue, cost, profit, margin);
        })
        .OrderByDescending(r => r.Profit)
        .ToList();

        var totRev = rows.Sum(r => r.Revenue);
        var totCost = rows.Sum(r => r.Cost);
        var totProfit = totRev - totCost;
        var totMargin = totRev != 0 ? Math.Round(totProfit / totRev * 100, 1) : 0m;
        return new ProductProfitResult(rows, totRev, totCost, totProfit, totMargin);
    }
}
