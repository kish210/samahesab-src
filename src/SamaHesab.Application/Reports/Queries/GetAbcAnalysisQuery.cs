using MediatR;
using SamaHesab.Application.BI.Queries;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.Inventory;
using SamaHesab.Domain.Entities.Sales;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Reports.Queries;

/// <summary>یک ردیفِ تحلیلِ ABC کالا.</summary>
public record AbcRow(string Code, string Name, decimal Value, decimal SharePercent, decimal CumulativePercent, string Class);

public record AbcResult(IReadOnlyList<AbcRow> Rows,
    int CountA, int CountB, int CountC, decimal TotalValue);

/// <summary>
/// فاز ۱۲ (پولیش) — تحلیلِ ABCِ کالا بر اساسِ ارزشِ فروش در بازه (اقلامِ فاکتورِ فروشِ قطعی).
/// طبقه‌بندیِ پارِتو: A (مهم‌ترین ۸۰٪ ارزش) · B (۸۰–۹۵٪) · C (۹۵–۱۰۰٪).
/// </summary>
public record GetAbcAnalysisQuery(string FromDate, string ToDate) : IRequest<AbcResult>;

public class GetAbcAnalysisQueryHandler : IRequestHandler<GetAbcAnalysisQuery, AbcResult>
{
    private readonly IRepository<SalesInvoice> _invoices;
    private readonly IRepository<SalesInvoiceItem> _items;
    private readonly IRepository<Product> _products;
    private readonly ICurrentUserService _user;

    public GetAbcAnalysisQueryHandler(IRepository<SalesInvoice> invoices, IRepository<SalesInvoiceItem> items,
        IRepository<Product> products, ICurrentUserService user)
    { _invoices = invoices; _items = items; _products = products; _user = user; }

    public async Task<AbcResult> Handle(GetAbcAnalysisQuery req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        var sales = await SalesQueryHelper.LoadSalesAsync(_invoices, companyId, req.FromDate, req.ToDate, ct);
        var ids = sales.Select(s => s.Id).ToHashSet();
        var items = await _items.FindAsync(it => ids.Contains(it.InvoiceId), ct);

        var valueByProduct = items.GroupBy(it => it.ProductId)
            .Select(g => new SamaHesab.Application.Reports.AbcInput(g.Key, g.Sum(x => x.NetAmount)))
            .ToList();

        var classified = AbcEngine.Classify(valueByProduct);

        var names = (await _products.FindAsync(p => p.CompanyId == companyId, ct))
            .ToDictionary(p => p.Id, p => (p.Code, p.Name));

        var rows = classified.Select(c =>
        {
            var (code, name) = names.TryGetValue(c.Id, out var p) ? p : ($"#{c.Id}", "");
            return new AbcRow(code, name, c.Value, c.SharePercent, c.CumulativePercent, c.Class.ToString());
        }).ToList();

        return new AbcResult(rows,
            rows.Count(r => r.Class == "A"), rows.Count(r => r.Class == "B"), rows.Count(r => r.Class == "C"),
            rows.Sum(r => r.Value));
    }
}
