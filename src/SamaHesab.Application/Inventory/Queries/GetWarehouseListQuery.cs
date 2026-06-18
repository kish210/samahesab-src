using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Inventory.Queries;

/// <summary>فهرستِ کاملِ انبارها (صفحهٔ مدیریت) — منبعِ واحد (API + دسکتاپ). الگوی API-only.</summary>
public record WarehouseRowDto(int Id, string Code, string Name, string Manager, string Address, bool IsDefault, bool IsActive);

public record GetWarehouseListQuery : IRequest<List<WarehouseRowDto>>;

public class GetWarehouseListQueryHandler : IRequestHandler<GetWarehouseListQuery, List<WarehouseRowDto>>
{
    private readonly IWarehouseRepository _warehouses;
    private readonly ICurrentUserService _currentUser;
    public GetWarehouseListQueryHandler(IWarehouseRepository warehouses, ICurrentUserService currentUser)
    { _warehouses = warehouses; _currentUser = currentUser; }

    public async Task<List<WarehouseRowDto>> Handle(GetWarehouseListQuery req, CancellationToken ct)
    {
        var companyId = _currentUser.CompanyId ?? 1;
        var list = await _warehouses.GetByCompanyAsync(companyId, ct);
        return list.Select(w => new WarehouseRowDto(w.Id, w.Code, w.Name, w.ManagerName ?? "",
            w.Address ?? "", w.IsDefault, w.IsActive)).ToList();
    }
}
