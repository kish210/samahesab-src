using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Entities.POS;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.POS;

// ── تعلیق فاکتور ──────────────────────────────────────────────────────────────
public record HoldSaleCommand(string Label, string Payload, decimal Total) : IRequest<Result<int>>;

public class HoldSaleCommandHandler : IRequestHandler<HoldSaleCommand, Result<int>>
{
    private readonly IRepository<HeldSale> _held; private readonly IUnitOfWork _uow; private readonly ICurrentUserService _user;
    public HoldSaleCommandHandler(IRepository<HeldSale> held, IUnitOfWork uow, ICurrentUserService user)
    { _held = held; _uow = uow; _user = user; }

    public async Task<Result<int>> Handle(HoldSaleCommand req, CancellationToken ct)
    {
        try
        {
            var h = HeldSale.Create(_user.CompanyId ?? 1, _user.BranchId ?? 1, _user.UserId ?? 0, req.Label, req.Payload, req.Total);
            await _held.AddAsync(h, ct); await _uow.SaveChangesAsync(ct);
            return Result<int>.Success(h.Id);
        }
        catch (Exception ex) { return Result<int>.Failure(ex.GetBaseException().Message); }
    }
}

// ── حذف (پس از فراخوان یا انصراف) ─────────────────────────────────────────────
public record DeleteHeldSaleCommand(int Id) : IRequest<Result>;

public class DeleteHeldSaleCommandHandler : IRequestHandler<DeleteHeldSaleCommand, Result>
{
    private readonly IRepository<HeldSale> _held; private readonly IUnitOfWork _uow;
    public DeleteHeldSaleCommandHandler(IRepository<HeldSale> held, IUnitOfWork uow) { _held = held; _uow = uow; }

    public async Task<Result> Handle(DeleteHeldSaleCommand req, CancellationToken ct)
    {
        var h = await _held.GetByIdAsync(req.Id, ct);
        if (h is null) return Result.Failure("فاکتور معلق یافت نشد.");
        _held.Remove(h); await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}

// ── فهرست فاکتورهای معلقِ کاربر ───────────────────────────────────────────────
public record HeldSaleListDto(int Id, string Label, decimal Total, DateTime CreatedAt);
public record GetHeldSalesQuery() : IRequest<List<HeldSaleListDto>>;

public class GetHeldSalesQueryHandler : IRequestHandler<GetHeldSalesQuery, List<HeldSaleListDto>>
{
    private readonly IRepository<HeldSale> _held; private readonly ICurrentUserService _user;
    public GetHeldSalesQueryHandler(IRepository<HeldSale> held, ICurrentUserService user) { _held = held; _user = user; }

    public async Task<List<HeldSaleListDto>> Handle(GetHeldSalesQuery req, CancellationToken ct)
    {
        var userId = _user.UserId ?? 0;
        var list = await _held.FindAsync(h => h.UserId == userId, ct);
        return list.OrderByDescending(h => h.CreatedAt)
            .Select(h => new HeldSaleListDto(h.Id, h.Label, h.Total, h.CreatedAt)).ToList();
    }
}

// ── فراخوان: گرفتن سبدِ ذخیره‌شده ─────────────────────────────────────────────
public record HeldSaleDetailDto(int Id, string Label, string Payload, decimal Total);
public record GetHeldSaleQuery(int Id) : IRequest<HeldSaleDetailDto?>;

public class GetHeldSaleQueryHandler : IRequestHandler<GetHeldSaleQuery, HeldSaleDetailDto?>
{
    private readonly IRepository<HeldSale> _held;
    public GetHeldSaleQueryHandler(IRepository<HeldSale> held) => _held = held;

    public async Task<HeldSaleDetailDto?> Handle(GetHeldSaleQuery req, CancellationToken ct)
    {
        var h = await _held.GetByIdAsync(req.Id, ct);
        return h is null ? null : new HeldSaleDetailDto(h.Id, h.Label, h.Payload, h.Total);
    }
}
