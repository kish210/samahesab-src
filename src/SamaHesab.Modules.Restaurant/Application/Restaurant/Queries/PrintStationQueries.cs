using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Interfaces.Repositories;
using SamaHesab.Modules.Restaurant.Domain;

namespace SamaHesab.Modules.Restaurant.Application.Queries;

/// <summary>فهرستِ ایستگاه‌های چاپ (فیش‌پرینترها).</summary>
public record GetPrintStationsQuery(bool ActiveOnly = false) : IRequest<List<PrintStationDto>>;

public record PrintStationDto(int Id, string Name, string PrinterName, bool IsDefault, bool Active);

public class GetPrintStationsQueryHandler : IRequestHandler<GetPrintStationsQuery, List<PrintStationDto>>
{
    private readonly IRepository<PrintStation> _stations;
    private readonly ICurrentUserService _user;

    public GetPrintStationsQueryHandler(IRepository<PrintStation> stations, ICurrentUserService user)
    { _stations = stations; _user = user; }

    public async Task<List<PrintStationDto>> Handle(GetPrintStationsQuery req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        var rows = await _stations.FindAsync(s => s.CompanyId == companyId && (!req.ActiveOnly || s.Active), ct);
        return rows
            .OrderByDescending(s => s.IsDefault).ThenBy(s => s.Name)
            .Select(s => new PrintStationDto(s.Id, s.Name, s.PrinterName, s.IsDefault, s.Active))
            .ToList();
    }
}

/// <summary>نگاشتِ کالا→ایستگاه (برای صفحهٔ تنظیمات و مسیریابیِ چاپ). خروجی: کلیدِ کالا، مقدارِ ایستگاه.</summary>
public record GetProductStationMapQuery() : IRequest<Dictionary<int, int>>;

public class GetProductStationMapQueryHandler : IRequestHandler<GetProductStationMapQuery, Dictionary<int, int>>
{
    private readonly IRepository<ProductStationMap> _maps;
    private readonly ICurrentUserService _user;

    public GetProductStationMapQueryHandler(IRepository<ProductStationMap> maps, ICurrentUserService user)
    { _maps = maps; _user = user; }

    public async Task<Dictionary<int, int>> Handle(GetProductStationMapQuery req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        var rows = await _maps.FindAsync(m => m.CompanyId == companyId, ct);
        return rows.GroupBy(m => m.ProductId).ToDictionary(g => g.Key, g => g.First().StationId);
    }
}
