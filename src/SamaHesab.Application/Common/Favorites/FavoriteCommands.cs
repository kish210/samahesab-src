using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Entities.Settings;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Common.Favorites;

// ── ثبت استفاده (برای فهرست «اخیر») — upsert ──────────────────────────────────
public record TouchRecentItemCommand(string EntityType, int EntityId, string Label) : IRequest<Result>;

public class TouchRecentItemCommandHandler : IRequestHandler<TouchRecentItemCommand, Result>
{
    private readonly IUserItemRefRepository _refs;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;
    public TouchRecentItemCommandHandler(IUserItemRefRepository refs, IUnitOfWork uow, ICurrentUserService user)
    { _refs = refs; _uow = uow; _user = user; }

    public async Task<Result> Handle(TouchRecentItemCommand req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1; var userId = _user.UserId ?? 0;
        try
        {
            var existing = await _refs.FindAsync(companyId, userId, req.EntityType, req.EntityId, ct);
            if (existing is null)
                await _refs.AddAsync(UserItemRef.Create(companyId, userId, req.EntityType, req.EntityId, req.Label), ct);
            else { existing.Touch(req.Label); _refs.Update(existing); }
            await _uow.SaveChangesAsync(ct);
            return Result.Success();
        }
        catch (Exception ex) { return Result.Failure(ex.GetBaseException().Message); }
    }
}

// ── سنجاق‌کردن/برداشتن سنجاق (Favorite/Pinned) — upsert ───────────────────────
public record SetPinnedItemCommand(string EntityType, int EntityId, string Label, bool Pinned) : IRequest<Result>;

public class SetPinnedItemCommandHandler : IRequestHandler<SetPinnedItemCommand, Result>
{
    private readonly IUserItemRefRepository _refs;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;
    public SetPinnedItemCommandHandler(IUserItemRefRepository refs, IUnitOfWork uow, ICurrentUserService user)
    { _refs = refs; _uow = uow; _user = user; }

    public async Task<Result> Handle(SetPinnedItemCommand req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1; var userId = _user.UserId ?? 0;
        try
        {
            var existing = await _refs.FindAsync(companyId, userId, req.EntityType, req.EntityId, ct);
            if (existing is null)
            {
                var item = UserItemRef.Create(companyId, userId, req.EntityType, req.EntityId, req.Label);
                item.SetPinned(req.Pinned);
                await _refs.AddAsync(item, ct);
            }
            else { existing.SetPinned(req.Pinned, req.Label); _refs.Update(existing); }
            await _uow.SaveChangesAsync(ct);
            return Result.Success();
        }
        catch (Exception ex) { return Result.Failure(ex.GetBaseException().Message); }
    }
}
