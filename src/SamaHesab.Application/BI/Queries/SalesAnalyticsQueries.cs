using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Domain.Entities.Sales;
using SamaHesab.Domain.Enums;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.BI.Queries;

// ── DTOs ─────────────────────────────────────────────────────────────────────
public record TopCustomerDto(int CustomerId, string Name, decimal Total, int InvoiceCount);
public record TopProductDto(int ProductId, string Name, decimal Total, int LineCount, decimal Profit);
public record SalesTrendDto(string Period, decimal Total, int Count);
public record ProfitAnalysisDto(decimal Sales, decimal Profit, decimal MarginPercent, List<TopProductDto> TopProducts);

// ── Helper: load posted/confirmed sales headers in a Persian-date range ──────
internal static class SalesQueryHelper
{
    public static async Task<List<SalesInvoice>> LoadSalesAsync(
        IRepository<SalesInvoice> repo, int companyId, string from, string to, CancellationToken ct)
    {
        // فیلتر شرکت/نوع/وضعیت در SQL؛ بازه‌ی تاریخِ شمسی در حافظه (مقایسه‌ی لغوی پایدار)
        var all = await repo.FindAsync(
            i => i.CompanyId == companyId
                 && i.InvoiceType == InvoiceType.Sale
                 && i.Status != InvoiceStatus.Draft
                 && i.Status != InvoiceStatus.Cancelled, ct);
        return all
            .Where(i => string.CompareOrdinal(i.InvoiceDate, from) >= 0
                     && string.CompareOrdinal(i.InvoiceDate, to) <= 0)
            .ToList();
    }
}

// ── Top Customers ────────────────────────────────────────────────────────────
public record GetTopCustomersQuery(string FromDate, string ToDate, int Take = 10)
    : IRequest<List<TopCustomerDto>>;

public class GetTopCustomersQueryHandler : IRequestHandler<GetTopCustomersQuery, List<TopCustomerDto>>
{
    private readonly IRepository<SalesInvoice> _invoices;
    private readonly IRepository<Customer> _customers;
    private readonly ICurrentUserService _currentUser;

    public GetTopCustomersQueryHandler(IRepository<SalesInvoice> invoices,
        IRepository<Customer> customers, ICurrentUserService currentUser)
    { _invoices = invoices; _customers = customers; _currentUser = currentUser; }

    public async Task<List<TopCustomerDto>> Handle(GetTopCustomersQuery req, CancellationToken ct)
    {
        var companyId = _currentUser.CompanyId ?? 1;
        var sales = await SalesQueryHelper.LoadSalesAsync(_invoices, companyId, req.FromDate, req.ToDate, ct);

        var ranked = SalesAnalytics.TopParties(
            sales.Select(i => new SalesRecord(i.InvoiceDate, i.CustomerId, i.GrandTotal)), req.Take);

        var names = (await _customers.FindAsync(c => c.CompanyId == companyId, ct))
            .ToDictionary(c => c.Id, c => c.FullName);

        return ranked
            .Select(r => new TopCustomerDto(r.Id,
                names.TryGetValue(r.Id, out var n) ? n : $"#{r.Id}", r.Total, r.Count))
            .ToList();
    }
}

// ── Sales Trend (monthly) ────────────────────────────────────────────────────
public record GetSalesTrendQuery(string FromDate, string ToDate) : IRequest<List<SalesTrendDto>>;

public class GetSalesTrendQueryHandler : IRequestHandler<GetSalesTrendQuery, List<SalesTrendDto>>
{
    private readonly IRepository<SalesInvoice> _invoices;
    private readonly ICurrentUserService _currentUser;

    public GetSalesTrendQueryHandler(IRepository<SalesInvoice> invoices, ICurrentUserService currentUser)
    { _invoices = invoices; _currentUser = currentUser; }

    public async Task<List<SalesTrendDto>> Handle(GetSalesTrendQuery req, CancellationToken ct)
    {
        var companyId = _currentUser.CompanyId ?? 1;
        var sales = await SalesQueryHelper.LoadSalesAsync(_invoices, companyId, req.FromDate, req.ToDate, ct);
        return SalesAnalytics
            .MonthlyTrend(sales.Select(i => new SalesRecord(i.InvoiceDate, i.CustomerId, i.GrandTotal)))
            .Select(p => new SalesTrendDto(p.Period, p.Total, p.Count))
            .ToList();
    }
}

// ── Profit Analysis (+ top products) ─────────────────────────────────────────
public record GetProfitAnalysisQuery(string FromDate, string ToDate, int TopProducts = 10)
    : IRequest<ProfitAnalysisDto>;

public class GetProfitAnalysisQueryHandler : IRequestHandler<GetProfitAnalysisQuery, ProfitAnalysisDto>
{
    private readonly IRepository<SalesInvoice> _invoices;
    private readonly IRepository<SalesInvoiceItem> _items;
    private readonly IRepository<SamaHesab.Domain.Entities.Inventory.Product> _products;
    private readonly ICurrentUserService _currentUser;

    public GetProfitAnalysisQueryHandler(IRepository<SalesInvoice> invoices,
        IRepository<SalesInvoiceItem> items,
        IRepository<SamaHesab.Domain.Entities.Inventory.Product> products,
        ICurrentUserService currentUser)
    { _invoices = invoices; _items = items; _products = products; _currentUser = currentUser; }

    public async Task<ProfitAnalysisDto> Handle(GetProfitAnalysisQuery req, CancellationToken ct)
    {
        var companyId = _currentUser.CompanyId ?? 1;
        var sales = await SalesQueryHelper.LoadSalesAsync(_invoices, companyId, req.FromDate, req.ToDate, ct);
        var ids = sales.Select(s => s.Id).ToHashSet();
        var totalSales = sales.Sum(s => s.GrandTotal);

        var allItems = await _items.FindAsync(it => ids.Contains(it.InvoiceId), ct);
        var productSales = allItems
            .Select(it => new ProductSale(it.ProductId, it.Quantity, it.NetAmount, it.Profit ?? 0));

        var profit = SalesAnalytics.TotalProfit(productSales);
        var topRanked = SalesAnalytics.TopProducts(productSales, req.TopProducts);

        var names = (await _products.FindAsync(p => p.CompanyId == companyId, ct))
            .ToDictionary(p => p.Id, p => p.Name);

        var top = topRanked
            .Select(r => new TopProductDto(r.Id,
                names.TryGetValue(r.Id, out var n) ? n : $"#{r.Id}", r.Total, r.Count, r.Extra))
            .ToList();

        var margin = totalSales == 0 ? 0 : Math.Round(profit / totalSales * 100, 2);
        return new ProfitAnalysisDto(totalSales, profit, margin, top);
    }
}
