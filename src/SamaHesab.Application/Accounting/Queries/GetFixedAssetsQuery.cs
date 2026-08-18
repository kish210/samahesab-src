using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Domain.Enums;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Accounting.Queries;

public record FixedAssetDto(
    int Id, string Code, string Name, string PurchaseDate, decimal PurchaseCost, decimal SalvageValue,
    int UsefulLifeMonths, DepreciationMethod Method, bool IsActive, decimal AccumulatedDepreciation,
    decimal BookValue, decimal MonthlyDepreciation, bool IsFullyDepreciated, string? Description);

public record GetFixedAssetsQuery() : IRequest<List<FixedAssetDto>>;

public class GetFixedAssetsQueryHandler : IRequestHandler<GetFixedAssetsQuery, List<FixedAssetDto>>
{
    private readonly IRepository<FixedAsset> _assets;
    private readonly ICurrentUserService _user;

    public GetFixedAssetsQueryHandler(IRepository<FixedAsset> assets, ICurrentUserService user)
    { _assets = assets; _user = user; }

    public async Task<List<FixedAssetDto>> Handle(GetFixedAssetsQuery request, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 0;
        var assets = await _assets.FindAsync(a => a.CompanyId == companyId, ct);

        return assets
            .OrderBy(a => a.Code)
            .Select(a =>
            {
                var monthly = a.Method == DepreciationMethod.StraightLine
                    ? DepreciationCalculator.MonthlyStraightLine(a.PurchaseCost, a.SalvageValue, a.UsefulLifeMonths)
                    : DepreciationCalculator.MonthlyDecliningBalance(a.PurchaseCost, a.SalvageValue, a.UsefulLifeMonths, a.BookValue);
                return new FixedAssetDto(
                    a.Id, a.Code, a.Name, a.PurchaseDate, a.PurchaseCost, a.SalvageValue,
                    a.UsefulLifeMonths, a.Method, a.IsActive, a.AccumulatedDepreciation,
                    a.BookValue, monthly, a.IsFullyDepreciated, a.Description);
            })
            .ToList();
    }
}
