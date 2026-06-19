using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Domain.Entities.Sales;
using SamaHesab.Domain.Enums;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.BI.Queries;

/// <summary>تحلیل ۳۶۰درجه‌ی یک مشتری — فاز محصول‌سازی (Customer Analytics).</summary>
public record GetCustomerAnalyticsQuery(int CustomerId, string FromDate, string ToDate)
    : IRequest<CustomerAnalyticsDto>;

public record CustomerAnalyticsDto(
    int CustomerId,
    string Name,
    decimal Balance,
    decimal TotalSales,
    int InvoiceCount,
    decimal AveragePerInvoice,
    string? FirstInvoiceDate,
    string? LastInvoiceDate,
    List<SalesTrendDto> MonthlyTrend,
    List<TopProductDto> TopProducts);

public class GetCustomerAnalyticsQueryHandler
    : IRequestHandler<GetCustomerAnalyticsQuery, CustomerAnalyticsDto>
{
    private readonly IRepository<SalesInvoice> _invoices;
    private readonly IRepository<SalesInvoiceItem> _items;
    private readonly IRepository<Party> _customers;
    private readonly IRepository<SamaHesab.Domain.Entities.Inventory.Product> _products;
    private readonly ICurrentUserService _currentUser;

    public GetCustomerAnalyticsQueryHandler(IRepository<SalesInvoice> invoices,
        IRepository<SalesInvoiceItem> items, IRepository<Party> customers,
        IRepository<SamaHesab.Domain.Entities.Inventory.Product> products, ICurrentUserService currentUser)
    { _invoices = invoices; _items = items; _customers = customers; _products = products; _currentUser = currentUser; }

    public async Task<CustomerAnalyticsDto> Handle(GetCustomerAnalyticsQuery req, CancellationToken ct)
    {
        var companyId = _currentUser.CompanyId ?? 1;
        var customer = await _customers.FindSingleAsync(
            c => c.CompanyId == companyId && c.Id == req.CustomerId, ct);

        var all = await _invoices.FindAsync(
            i => i.CompanyId == companyId && i.CustomerId == req.CustomerId
                 && i.InvoiceType == InvoiceType.Sale
                 && i.Status != InvoiceStatus.Draft && i.Status != InvoiceStatus.Cancelled, ct);
        var sales = all
            .Where(i => string.CompareOrdinal(i.InvoiceDate, req.FromDate) >= 0
                     && string.CompareOrdinal(i.InvoiceDate, req.ToDate) <= 0)
            .ToList();

        var total = sales.Sum(i => i.GrandTotal);
        var count = sales.Count;
        var avg = count == 0 ? 0 : Math.Round(total / count, 0);
        var dates = sales.Select(i => i.InvoiceDate).OrderBy(d => d, StringComparer.Ordinal).ToList();

        var trend = SalesAnalytics
            .MonthlyTrend(sales.Select(i => new SalesRecord(i.InvoiceDate, i.CustomerId, i.GrandTotal)))
            .Select(p => new SalesTrendDto(p.Period, p.Total, p.Count))
            .ToList();

        var ids = sales.Select(s => s.Id).ToHashSet();
        var lineItems = await _items.FindAsync(it => ids.Contains(it.InvoiceId), ct);
        var topRanked = SalesAnalytics.TopProducts(
            lineItems.Select(it => new ProductSale(it.ProductId, it.Quantity, it.NetAmount, it.Profit ?? 0)), 10);
        var names = (await _products.FindAsync(p => p.CompanyId == companyId, ct))
            .ToDictionary(p => p.Id, p => p.Name);
        var topProducts = topRanked
            .Select(r => new TopProductDto(r.Id, names.TryGetValue(r.Id, out var n) ? n : $"#{r.Id}",
                r.Total, r.Count, r.Extra))
            .ToList();

        return new CustomerAnalyticsDto(
            req.CustomerId, customer?.FullName ?? $"#{req.CustomerId}", customer?.Balance ?? 0,
            total, count, avg,
            dates.FirstOrDefault(), dates.LastOrDefault(), trend, topProducts);
    }
}
