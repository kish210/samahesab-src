using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Entities.Inventory;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Inventory.Commands;

/// <summary>
/// ساختِ انبار (منبعِ واحد: API + دسکتاپ). کد در صورتِ خالی‌بودن خودکار تولید می‌شود.
/// برای ویزاردِ راه‌اندازیِ اولیه و مدیریتِ انبارها استفاده می‌شود.
/// </summary>
public record CreateWarehouseCommand(string Name, string? Code = null) : IRequest<Result<int>>;

public class CreateWarehouseCommandHandler : IRequestHandler<CreateWarehouseCommand, Result<int>>
{
    private readonly IWarehouseRepository _warehouses;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public CreateWarehouseCommandHandler(IWarehouseRepository warehouses, IUnitOfWork unitOfWork,
        ICurrentUserService currentUser)
    { _warehouses = warehouses; _unitOfWork = unitOfWork; _currentUser = currentUser; }

    public async Task<Result<int>> Handle(CreateWarehouseCommand request, CancellationToken ct)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return Result<int>.Failure("نام انبار الزامی است.");

            var companyId = _currentUser.CompanyId ?? 1;
            var name = request.Name.Trim();
            var existing = await _warehouses.GetByCompanyAsync(companyId, ct);

            if (existing.Any(w => w.Name == name))
                return Result<int>.Failure("انباری با این نام موجود است.");

            var code = string.IsNullOrWhiteSpace(request.Code) ? $"W{existing.Count + 1:000}" : request.Code.Trim();
            while (existing.Any(w => w.Code == code))
                code = $"W{existing.Count + 1:000}-{Guid.NewGuid().ToString()[..4]}";

            var wh = Warehouse.Create(companyId, code, name);
            await _warehouses.AddAsync(wh, ct);
            await _unitOfWork.SaveChangesAsync(ct);
            return Result<int>.Success(wh.Id);
        }
        catch (Exception ex) { return Result<int>.Failure(ex.Message); }
    }
}
