using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.Inventory;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Automation.Queries;

/// <summary>
/// پیشنهاد خودکار سفارش خرید بر اساس نقطه‌ی سفارش/سقف موجودی — فاز محصول‌سازی (P4).
/// </summary>
public record GetReorderSuggestionsQuery() : IRequest<List<ReorderSuggestion>>;

public class GetReorderSuggestionsQueryHandler
    : IRequestHandler<GetReorderSuggestionsQuery, List<ReorderSuggestion>>
{
    private readonly IRepository<Product> _products;
    private readonly IRepository<StockItem> _stock;
    private readonly ICurrentUserService _currentUser;

    public GetReorderSuggestionsQueryHandler(IRepository<Product> products,
        IRepository<StockItem> stock, ICurrentUserService currentUser)
    { _products = products; _stock = stock; _currentUser = currentUser; }

    public async Task<List<ReorderSuggestion>> Handle(GetReorderSuggestionsQuery req, CancellationToken ct)
    {
        var companyId = _currentUser.CompanyId ?? 1;
        var products = await _products.FindAsync(
            p => p.CompanyId == companyId && (p.MinStock > 0 || p.ReorderPoint > 0), ct);
        var ids = products.Select(p => p.Id).ToList();

        var onHand = (await _stock.FindAsync(s => ids.Contains(s.ProductId), ct))
            .GroupBy(s => s.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

        var inputs = products.Select(p => new ReorderInput(
            p.Id, p.Name, onHand.TryGetValue(p.Id, out var q) ? q : 0,
            p.MinStock, p.ReorderPoint, p.MaxStock));

        return ReorderEngine.Suggest(inputs);
    }
}
