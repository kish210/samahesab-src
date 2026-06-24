using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Modules.Tourism.Domain;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Modules.Tourism.Application.Queries;

/// <summary>فهرستِ محصولات/خدماتِ گردشگری (برای انتخاب‌گرِ صفحهٔ فروش — TUR-C2-4).</summary>
public record GetTourismProductsQuery(bool ActiveOnly = true) : IRequest<List<TourismProductDto>>;

public record TourismProductDto(
    int Id, string Name, int SupplierPartyId, string SupplierName,
    decimal PurchasePrice, decimal DefaultSalePrice, int? ProductGroupId, bool RequiresPassengerList, bool Active);

public class GetTourismProductsQueryHandler : IRequestHandler<GetTourismProductsQuery, List<TourismProductDto>>
{
    private readonly IRepository<TourismProduct> _products;
    private readonly IRepository<Party> _parties;
    private readonly ICurrentUserService _user;

    public GetTourismProductsQueryHandler(IRepository<TourismProduct> products, IRepository<Party> parties,
        ICurrentUserService user)
    { _products = products; _parties = parties; _user = user; }

    public async Task<List<TourismProductDto>> Handle(GetTourismProductsQuery req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        var items = await _products.FindAsync(
            p => p.CompanyId == companyId && (!req.ActiveOnly || p.Active), ct);

        var supplierIds = items.Select(p => p.SupplierPartyId).ToHashSet();
        var names = (await _parties.FindAsync(p => supplierIds.Contains(p.Id), ct))
            .ToDictionary(p => p.Id, p => p.FullName);

        return items
            .OrderBy(p => p.Name)
            .Select(p => new TourismProductDto(
                p.Id, p.Name, p.SupplierPartyId, names.GetValueOrDefault(p.SupplierPartyId, $"#{p.SupplierPartyId}"),
                p.PurchasePrice, p.DefaultSalePrice, p.ProductGroupId, p.RequiresPassengerList, p.Active))
            .ToList();
    }
}
