using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.Inventory;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.BI.Queries;

/// <summary>روند ورود/خروج موجودی به‌تفکیک ماه — فاز محصول‌سازی (BI، روند موجودی).</summary>
public record GetInventoryTrendQuery(string FromDate, string ToDate, int? ProductId = null)
    : IRequest<List<InventoryTrendPoint>>;

public class GetInventoryTrendQueryHandler
    : IRequestHandler<GetInventoryTrendQuery, List<InventoryTrendPoint>>
{
    private readonly IRepository<StockTransaction> _ledger;
    private readonly ICurrentUserService _currentUser;

    public GetInventoryTrendQueryHandler(IRepository<StockTransaction> ledger, ICurrentUserService currentUser)
    { _ledger = ledger; _currentUser = currentUser; }

    public async Task<List<InventoryTrendPoint>> Handle(GetInventoryTrendQuery req, CancellationToken ct)
    {
        var companyId = _currentUser.CompanyId ?? 1;
        var rows = await _ledger.FindAsync(
            t => t.CompanyId == companyId
                 && (req.ProductId == null || t.ProductId == req.ProductId), ct);

        var inRange = rows.Where(t => string.CompareOrdinal(t.DocumentDate, req.FromDate) >= 0
                                   && string.CompareOrdinal(t.DocumentDate, req.ToDate) <= 0);

        return InventoryAnalytics.MonthlyMovement(
            inRange.Select(t => new StockMovement(t.DocumentDate, t.Quantity)));
    }
}
