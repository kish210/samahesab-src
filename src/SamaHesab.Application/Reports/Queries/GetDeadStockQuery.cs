using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.Inventory;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Reports.Queries;

/// <summary>یک ردیفِ گزارشِ کالای راکد.</summary>
public record DeadStockRow(string Code, string Name, decimal Quantity, decimal Value, string LastMovement, int IdleDays)
{
    /// <summary>نمایشِ روزِ رکود (برای کالای بدونِ حرکت، متنِ گویا به‌جای ۱-).</summary>
    public string IdleDisplay => IdleDays < 0 ? "بدونِ حرکت" : IdleDays.ToString();
}

/// <summary>
/// فاز ۱۲ (پولیش) — گزارشِ کالای راکد/کم‌گردش: کالاهایی که موجودی دارند ولی از آخرین حرکتِ
/// انبار (ورود/خروج) بیش از <paramref name="IdleDays"/> روز گذشته است. سرمایهٔ خوابیده را نشان می‌دهد.
/// </summary>
public record GetDeadStockQuery(int IdleDays = 90, string? AsOfDate = null) : IRequest<List<DeadStockRow>>;

public class GetDeadStockQueryHandler : IRequestHandler<GetDeadStockQuery, List<DeadStockRow>>
{
    private readonly IStockItemRepository _stock;
    private readonly IWarehouseRepository _warehouses;
    private readonly IProductRepository _products;
    private readonly IRepository<StockTransaction> _ledger;
    private readonly IPersianCalendarService _calendar;
    private readonly ICurrentUserService _user;

    public GetDeadStockQueryHandler(IStockItemRepository stock, IWarehouseRepository warehouses,
        IProductRepository products, IRepository<StockTransaction> ledger,
        IPersianCalendarService calendar, ICurrentUserService user)
    { _stock = stock; _warehouses = warehouses; _products = products; _ledger = ledger; _calendar = calendar; _user = user; }

    public async Task<List<DeadStockRow>> Handle(GetDeadStockQuery req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        DateTime asOf;
        try { asOf = string.IsNullOrWhiteSpace(req.AsOfDate) ? DateTime.Now : _calendar.ToGregorianDate(req.AsOfDate!); }
        catch { asOf = DateTime.Now; }

        // موجودیِ همهٔ انبارها
        var items = new List<StockItem>();
        foreach (var w in await _warehouses.GetByCompanyAsync(companyId, ct))
            items.AddRange(await _stock.GetByWarehouseAsync(w.Id, ct));

        var names = (await _products.SearchAsync(companyId, string.Empty, ct))
            .ToDictionary(p => p.Id, p => (p.Code, p.Name));

        // آخرین حرکتِ هر کالا (بیشینهٔ تاریخِ تراکنشِ انبار)
        var txns = await _ledger.FindAsync(t => t.CompanyId == companyId, ct);
        var lastMove = txns.GroupBy(t => t.ProductId).ToDictionary(g => g.Key, g => g.Max(t => t.CreatedAt));

        var rows = new List<DeadStockRow>();
        foreach (var g in items.GroupBy(s => s.ProductId))
        {
            var qty = g.Sum(x => x.Quantity);
            if (qty <= 0) continue;                                 // فقط کالای دارای موجودی
            var value = g.Sum(x => x.Quantity * x.AverageCost);
            var (code, name) = names.TryGetValue(g.Key, out var p) ? p : ($"#{g.Key}", "");

            int idle; string lastStr;
            if (lastMove.TryGetValue(g.Key, out var mv))
            { idle = Math.Max(0, (int)(asOf - mv).TotalDays); lastStr = _calendar.ToPersianDate(mv); }
            else { idle = int.MaxValue; lastStr = "بدونِ حرکت"; }    // هیچ تراکنشی نداشته

            if (idle >= req.IdleDays)
                rows.Add(new DeadStockRow(code, name, qty, value, lastStr, idle == int.MaxValue ? -1 : idle));
        }

        return rows.OrderByDescending(r => r.IdleDays < 0 ? int.MaxValue : r.IdleDays).ThenByDescending(r => r.Value).ToList();
    }
}
