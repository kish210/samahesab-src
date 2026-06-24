using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Modules.Tourism.Domain;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Modules.Tourism.Application.Queries;

/// <summary>
/// نمای ظرفیت/موجودیِ محصولاتِ گردشگری برای فروشنده — قیمت + ماندهٔ ظرفیت.
/// ماندهٔ ظرفیت از جمعِ تعدادِ خطوطِ فروشِ هر محصول کسر می‌شود.
/// </summary>
public record GetTourismAvailabilityQuery(bool OnlyActive = true) : IRequest<IReadOnlyList<TourismAvailabilityRow>>;

public class GetTourismAvailabilityQueryHandler
    : IRequestHandler<GetTourismAvailabilityQuery, IReadOnlyList<TourismAvailabilityRow>>
{
    private readonly IRepository<TourismProduct> _products;
    private readonly IRepository<TourismSaleLine> _lines;
    private readonly ICurrentUserService _user;

    public GetTourismAvailabilityQueryHandler(IRepository<TourismProduct> products,
        IRepository<TourismSaleLine> lines, ICurrentUserService user)
    { _products = products; _lines = lines; _user = user; }

    public async Task<IReadOnlyList<TourismAvailabilityRow>> Handle(GetTourismAvailabilityQuery req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;

        var products = (await _products.FindAsync(p => p.CompanyId == companyId, ct))
            .Select(p => new TourismProductInput(p.Id, p.Name, p.DefaultSalePrice, p.Capacity, p.Active))
            .ToList();

        var productIds = products.Select(p => p.ProductId).ToHashSet();

        // فروش‌رفته per محصول — فقط محصولاتِ همین شرکت (TourismSaleLine بدونِ CompanyId است).
        var sold = (await _lines.FindAsync(l => productIds.Contains(l.ProductId), ct))
            .GroupBy(l => l.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));

        return TourismAvailability.Build(products, sold, req.OnlyActive);
    }
}
