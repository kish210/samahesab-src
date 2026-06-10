using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Common.Favorites;

public record ItemRefDto(int EntityId, string Label, bool Pinned, int UseCount);

/// <summary>اخیرترین آیتم‌های یک نوع برای کاربر جاری (Recent).</summary>
public record GetRecentItemsQuery(string EntityType, int Top = 10) : IRequest<List<ItemRefDto>>;

public class GetRecentItemsQueryHandler : IRequestHandler<GetRecentItemsQuery, List<ItemRefDto>>
{
    private readonly IUserItemRefRepository _refs;
    private readonly ICurrentUserService _user;
    public GetRecentItemsQueryHandler(IUserItemRefRepository refs, ICurrentUserService user)
    { _refs = refs; _user = user; }

    public async Task<List<ItemRefDto>> Handle(GetRecentItemsQuery req, CancellationToken ct)
    {
        var list = await _refs.RecentAsync(_user.CompanyId ?? 1, _user.UserId ?? 0, req.EntityType,
            req.Top <= 0 ? 10 : req.Top, ct);
        return list.Select(r => new ItemRefDto(r.EntityId, r.Label, r.Pinned, r.UseCount)).ToList();
    }
}

/// <summary>آیتم‌های سنجاق‌شده‌ی یک نوع برای کاربر جاری (Pinned/Favorites).</summary>
public record GetPinnedItemsQuery(string EntityType) : IRequest<List<ItemRefDto>>;

public class GetPinnedItemsQueryHandler : IRequestHandler<GetPinnedItemsQuery, List<ItemRefDto>>
{
    private readonly IUserItemRefRepository _refs;
    private readonly ICurrentUserService _user;
    public GetPinnedItemsQueryHandler(IUserItemRefRepository refs, ICurrentUserService user)
    { _refs = refs; _user = user; }

    public async Task<List<ItemRefDto>> Handle(GetPinnedItemsQuery req, CancellationToken ct)
    {
        var list = await _refs.PinnedAsync(_user.CompanyId ?? 1, _user.UserId ?? 0, req.EntityType, ct);
        return list.Select(r => new ItemRefDto(r.EntityId, r.Label, r.Pinned, r.UseCount)).ToList();
    }
}
