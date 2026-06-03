using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.Inventory;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Inventory.Queries;

public record KardexRow(string Date, string Type, string? DocumentNumber,
    decimal In, decimal Out, decimal Balance, decimal UnitCost, string? Notes);

public record GetKardexQuery(int ProductId, int? WarehouseId, string? FromDate, string? ToDate)
    : IRequest<List<KardexRow>>;

public class GetKardexQueryHandler : IRequestHandler<GetKardexQuery, List<KardexRow>>
{
    private readonly IRepository<StockTransaction> _ledger;
    private readonly ICurrentUserService _currentUser;

    public GetKardexQueryHandler(IRepository<StockTransaction> ledger, ICurrentUserService currentUser)
    { _ledger = ledger; _currentUser = currentUser; }

    public async Task<List<KardexRow>> Handle(GetKardexQuery req, CancellationToken ct)
    {
        var companyId = _currentUser.CompanyId ?? 1;
        var rows = await _ledger.FindAsync(t =>
            t.CompanyId == companyId &&
            t.ProductId == req.ProductId &&
            (req.WarehouseId == null || t.WarehouseId == req.WarehouseId), ct);

        return rows
            .Where(t => (req.FromDate == null || string.Compare(t.DocumentDate, req.FromDate) >= 0)
                     && (req.ToDate == null || string.Compare(t.DocumentDate, req.ToDate) <= 0))
            .OrderBy(t => t.DocumentDate).ThenBy(t => t.Id)
            .Select(t => new KardexRow(
                t.DocumentDate, t.TransactionType, t.DocumentNumber,
                t.Quantity > 0 ? t.Quantity : 0,
                t.Quantity < 0 ? -t.Quantity : 0,
                t.BalanceQty, t.UnitCost, t.Notes))
            .ToList();
    }
}
