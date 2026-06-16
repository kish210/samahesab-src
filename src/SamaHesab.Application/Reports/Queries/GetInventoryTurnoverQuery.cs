using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.Inventory;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Reports.Queries;

/// <summary>یک ردیفِ گزارشِ گردشِ موجودی.</summary>
public record TurnoverRow(string Code, string Name, decimal InventoryValue, decimal Cogs, decimal Ratio, decimal Days)
{
    /// <summary>نمایشِ روزِ ماندگاری («بی‌گردش» برای کالای بدونِ فروش).</summary>
    public string DaysDisplay => Days < 0 ? "بی‌گردش" : Days.ToString("0.#");
}

/// <summary>
/// فاز ۱۲ (پولیش) — گردشِ موجودی per کالا در یک بازه: COGS (از خروجی‌های انبار) نسبت به ارزشِ
/// موجودیِ فعلی. کالاهای کم‌گردش/بی‌گردش بالا می‌آیند.
/// </summary>
public record GetInventoryTurnoverQuery(string FromDate, string ToDate) : IRequest<List<TurnoverRow>>;

public class GetInventoryTurnoverQueryHandler : IRequestHandler<GetInventoryTurnoverQuery, List<TurnoverRow>>
{
    private readonly IStockItemRepository _stock;
    private readonly IWarehouseRepository _warehouses;
    private readonly IProductRepository _products;
    private readonly IRepository<StockTransaction> _ledger;
    private readonly IPersianCalendarService _calendar;
    private readonly ICurrentUserService _user;

    public GetInventoryTurnoverQueryHandler(IStockItemRepository stock, IWarehouseRepository warehouses,
        IProductRepository products, IRepository<StockTransaction> ledger,
        IPersianCalendarService calendar, ICurrentUserService user)
    { _stock = stock; _warehouses = warehouses; _products = products; _ledger = ledger; _calendar = calendar; _user = user; }

    public async Task<List<TurnoverRow>> Handle(GetInventoryTurnoverQuery req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        int periodDays;
        try
        {
            var f = _calendar.ToGregorianDate(req.FromDate);
            var t = _calendar.ToGregorianDate(req.ToDate);
            periodDays = Math.Max(1, (int)(t - f).TotalDays + 1);
        }
        catch { periodDays = 30; }

        // ارزشِ موجودیِ فعلیِ هر کالا (همهٔ انبارها)
        var items = new List<StockItem>();
        foreach (var w in await _warehouses.GetByCompanyAsync(companyId, ct))
            items.AddRange(await _stock.GetByWarehouseAsync(w.Id, ct));
        var invValue = items.GroupBy(s => s.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity * x.AverageCost));

        // COGS بازه از خروجی‌های فروش
        var outflows = await _ledger.FindAsync(
            t => t.CompanyId == companyId && t.Quantity < 0 && t.RelatedDocType == "SalesInvoice"
                 && string.Compare(t.DocumentDate, req.FromDate) >= 0 && string.Compare(t.DocumentDate, req.ToDate) <= 0, ct);
        var cogsByProduct = outflows.GroupBy(t => t.ProductId).ToDictionary(g => g.Key, g => g.Sum(t => -t.TotalCost));

        var names = (await _products.SearchAsync(companyId, string.Empty, ct))
            .ToDictionary(p => p.Id, p => (p.Code, p.Name));

        // کالاهایی که موجودی یا فروش دارند
        var pids = invValue.Keys.Union(cogsByProduct.Keys).ToList();
        var rows = new List<TurnoverRow>();
        foreach (var pid in pids)
        {
            var value = invValue.TryGetValue(pid, out var v) ? v : 0m;
            var cogs = cogsByProduct.TryGetValue(pid, out var c) ? c : 0m;
            if (value <= 0 && cogs <= 0) continue;
            var tr = InventoryTurnover.Compute(cogs, value, periodDays);
            var (code, name) = names.TryGetValue(pid, out var p) ? p : ($"#{pid}", "");
            rows.Add(new TurnoverRow(code, name, value, cogs, tr.Ratio, tr.DaysOnHand));
        }

        // کم‌گردش‌ها (روزِ ماندگاریِ زیاد یا بی‌گردش=-۱) اول
        return rows.OrderByDescending(r => r.Days < 0 ? decimal.MaxValue : r.Days).ToList();
    }
}
