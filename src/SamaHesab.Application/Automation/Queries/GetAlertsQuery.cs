using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Domain.Entities.Inventory;
using SamaHesab.Domain.Enums;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Automation.Queries;

/// <summary>
/// اعلان‌های عملیاتی لحظه‌ای برای داشبورد/زنگوله — فاز محصول‌سازی (P2).
/// منبع: چک‌های سررسید (در جریان) + کسری موجودی کالا.
/// </summary>
public record GetAlertsQuery(string Today) : IRequest<List<Alert>>;

public class GetAlertsQueryHandler : IRequestHandler<GetAlertsQuery, List<Alert>>
{
    private readonly IChequeRepository _cheques;
    private readonly IRepository<Product> _products;
    private readonly IRepository<StockItem> _stock;
    private readonly ICurrentUserService _currentUser;

    public GetAlertsQueryHandler(IChequeRepository cheques, IRepository<Product> products,
        IRepository<StockItem> stock, ICurrentUserService currentUser)
    { _cheques = cheques; _products = products; _stock = stock; _currentUser = currentUser; }

    public async Task<List<Alert>> Handle(GetAlertsQuery req, CancellationToken ct)
    {
        var companyId = _currentUser.CompanyId ?? 1;

        var cheques = (await _cheques.GetByStatusAsync(companyId, ChequeStatus.InProcess, ct))
            .Select(c => new ChequeAlertInput(c.Id, c.ChequeNumber, c.DueDate, c.Amount,
                c.ChequeType.ToString()));

        // فقط کالاهایی که آستانه‌ی موجودی دارند
        var products = (await _products.FindAsync(
            p => p.CompanyId == companyId && (p.MinStock > 0 || p.ReorderPoint > 0), ct));
        var productIds = products.Select(p => p.Id).ToList();

        // StockItem فاقد CompanyId است؛ بر اساس کالاهای همان شرکت فیلتر می‌شود
        var onHand = (await _stock.FindAsync(s => productIds.Contains(s.ProductId), ct))
            .GroupBy(s => s.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

        var stockInputs = products.Select(p => new StockAlertInput(
            p.Id, p.Name, onHand.TryGetValue(p.Id, out var q) ? q : 0, p.MinStock, p.ReorderPoint));

        return AlertEngine.Build(cheques, req.Today, stockInputs);
    }
}
