using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Entities.Inventory;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Inventory.DiscountTiers;

public record DiscountTierDto(decimal MinQty, decimal DiscountPercent);

// ── دریافتِ پله‌های یک کالا (مرتب بر MinQty) ──
public record GetProductDiscountTiersQuery(int ProductId) : IRequest<List<DiscountTierDto>>;

public class GetProductDiscountTiersQueryHandler
    : IRequestHandler<GetProductDiscountTiersQuery, List<DiscountTierDto>>
{
    private readonly IRepository<ProductDiscountTier> _repo;
    private readonly ICurrentUserService _user;
    public GetProductDiscountTiersQueryHandler(IRepository<ProductDiscountTier> repo, ICurrentUserService user)
    { _repo = repo; _user = user; }

    public async Task<List<DiscountTierDto>> Handle(GetProductDiscountTiersQuery req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        var list = await _repo.FindAsync(t => t.CompanyId == companyId && t.ProductId == req.ProductId, ct);
        return list.OrderBy(t => t.MinQty)
            .Select(t => new DiscountTierDto(t.MinQty, t.DiscountPercent)).ToList();
    }
}

// ── جایگزینیِ کاملِ پله‌های یک کالا ──
public record SaveProductDiscountTiersCommand(int ProductId, List<DiscountTierDto> Tiers) : IRequest<Result>;

public class SaveProductDiscountTiersCommandHandler
    : IRequestHandler<SaveProductDiscountTiersCommand, Result>
{
    private readonly IRepository<ProductDiscountTier> _repo;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;
    public SaveProductDiscountTiersCommandHandler(IRepository<ProductDiscountTier> repo, IUnitOfWork uow, ICurrentUserService user)
    { _repo = repo; _uow = uow; _user = user; }

    public async Task<Result> Handle(SaveProductDiscountTiersCommand req, CancellationToken ct)
    {
        if (req.ProductId <= 0) return Result.Failure("کالا نامعتبر است.");
        var companyId = _user.CompanyId ?? 1;

        var existing = await _repo.FindAsync(t => t.CompanyId == companyId && t.ProductId == req.ProductId, ct);
        _repo.RemoveRange(existing);

        foreach (var t in req.Tiers.Where(t => t.MinQty > 0))
        {
            try { await _repo.AddAsync(ProductDiscountTier.Create(companyId, req.ProductId, t.MinQty, t.DiscountPercent), ct); }
            catch (System.Exception ex) { return Result.Failure(ex.Message); }
        }
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}

// ── حلِ بهترین تخفیف برای (کالا، مقدار): بزرگ‌ترین پلهٔ ≤ مقدار ──
public record ResolveQtyDiscountQuery(int ProductId, decimal Quantity) : IRequest<decimal>;

public class ResolveQtyDiscountQueryHandler : IRequestHandler<ResolveQtyDiscountQuery, decimal>
{
    private readonly IRepository<ProductDiscountTier> _repo;
    private readonly ICurrentUserService _user;
    public ResolveQtyDiscountQueryHandler(IRepository<ProductDiscountTier> repo, ICurrentUserService user)
    { _repo = repo; _user = user; }

    public async Task<decimal> Handle(ResolveQtyDiscountQuery req, CancellationToken ct)
    {
        if (req.Quantity <= 0) return 0;
        var companyId = _user.CompanyId ?? 1;
        var tiers = await _repo.FindAsync(
            t => t.CompanyId == companyId && t.ProductId == req.ProductId && t.MinQty <= req.Quantity, ct);
        return tiers.Count == 0 ? 0 : tiers.Max(t => t.DiscountPercent);
    }
}
