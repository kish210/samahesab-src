using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Domain.Interfaces.Repositories;
using SamaHesab.Modules.Tourism.Domain;

namespace SamaHesab.Modules.Tourism.Application.Itinerary.Queries;

/// <summary>فهرستِ محصولاتِ اقامتی همراهِ سانس‌هایشان (برای صفحهٔ مدیریت + برنامه‌ریز).</summary>
public record GetItineraryProductsQuery(bool ActiveOnly = true) : IRequest<List<ItineraryProductDto>>;

public record ItineraryProductSessionDto(int Id, string Label, int StartMinute, int EndMinute, int Capacity, bool Active);

public record ItineraryProductDto(
    int Id, string Name, decimal SalePrice, decimal Cost, decimal NetProfit, int Capacity,
    int? SupplierPartyId, string SupplierName, bool Active,
    Domain.CommissionBasis MarketerCommissionBasis, decimal MarketerCommissionValue, decimal MarketerCommission,
    IReadOnlyList<ItineraryProductSessionDto> Sessions);

public class GetItineraryProductsQueryHandler : IRequestHandler<GetItineraryProductsQuery, List<ItineraryProductDto>>
{
    private readonly IRepository<ItineraryProduct> _products;
    private readonly IRepository<ProductSession> _sessions;
    private readonly IRepository<Party> _parties;
    private readonly ICurrentUserService _user;

    public GetItineraryProductsQueryHandler(IRepository<ItineraryProduct> products,
        IRepository<ProductSession> sessions, IRepository<Party> parties, ICurrentUserService user)
    { _products = products; _sessions = sessions; _parties = parties; _user = user; }

    public async Task<List<ItineraryProductDto>> Handle(GetItineraryProductsQuery req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        var products = await _products.FindAsync(
            p => p.CompanyId == companyId && (!req.ActiveOnly || p.Active), ct);
        if (products.Count == 0) return new();

        var ids = products.Select(p => p.Id).ToHashSet();
        var sessions = await _sessions.FindAsync(
            s => s.CompanyId == companyId && ids.Contains(s.ProductId) && (!req.ActiveOnly || s.Active), ct);
        var byProduct = sessions
            .GroupBy(s => s.ProductId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<ItineraryProductSessionDto>)g
                .OrderBy(s => s.StartMinute)
                .Select(s => new ItineraryProductSessionDto(s.Id, s.Label, s.StartMinute, s.EndMinute, s.Capacity, s.Active))
                .ToList());

        // نامِ تأمین‌کننده‌ها (از اشخاص) برای نمایش.
        var supplierIds = products.Where(p => p.SupplierPartyId is int).Select(p => p.SupplierPartyId!.Value).ToHashSet();
        var names = supplierIds.Count == 0 ? new Dictionary<int, string>()
            : (await _parties.FindAsync(p => supplierIds.Contains(p.Id), ct)).ToDictionary(p => p.Id, p => p.FullName);

        return products
            .OrderBy(p => p.Name)
            .Select(p => new ItineraryProductDto(
                p.Id, p.Name, p.SalePrice, p.Cost, p.NetProfit, p.Capacity, p.SupplierPartyId,
                p.SupplierPartyId is int sid ? names.GetValueOrDefault(sid, $"#{sid}") : "",
                p.Active, p.MarketerCommissionBasis, p.MarketerCommissionValue, p.MarketerCommission,
                byProduct.GetValueOrDefault(p.Id, System.Array.Empty<ItineraryProductSessionDto>())))
            .ToList();
    }
}
