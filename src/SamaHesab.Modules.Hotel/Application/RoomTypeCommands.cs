using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Interfaces.Repositories;
using SamaHesab.Modules.Hotel.Domain;

namespace SamaHesab.Modules.Hotel.Application;

/// <summary>U-WEB-HOTEL — نوعِ اتاق (سوئیت/دوتخته/...). CQRSِ نو (ماژول قبلاً فقط دامنه داشت).</summary>
public record RoomTypeDto(int Id, string Code, string Name, int BaseCapacity, bool ExtraBedAllowed, bool Active);

public record GetRoomTypesQuery(bool IncludeInactive = false) : IRequest<List<RoomTypeDto>>;

public class GetRoomTypesQueryHandler : IRequestHandler<GetRoomTypesQuery, List<RoomTypeDto>>
{
    private readonly IRepository<RoomType> _repo;
    private readonly ICurrentUserService _user;
    public GetRoomTypesQueryHandler(IRepository<RoomType> repo, ICurrentUserService user) { _repo = repo; _user = user; }

    public async Task<List<RoomTypeDto>> Handle(GetRoomTypesQuery req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        var all = await _repo.FindAsync(r => r.CompanyId == companyId && (req.IncludeInactive || r.Active), ct);
        return all.OrderBy(r => r.Name)
            .Select(r => new RoomTypeDto(r.Id, r.Code, r.Name, r.BaseCapacity, r.ExtraBedAllowed, r.Active))
            .ToList();
    }
}

public record SaveRoomTypeCommand(int Id, string Code, string Name, int BaseCapacity, bool ExtraBedAllowed, bool Active) : IRequest<Result<int>>;

public class SaveRoomTypeCommandHandler : IRequestHandler<SaveRoomTypeCommand, Result<int>>
{
    private readonly IRepository<RoomType> _repo;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;
    public SaveRoomTypeCommandHandler(IRepository<RoomType> repo, IUnitOfWork uow, ICurrentUserService user)
    { _repo = repo; _uow = uow; _user = user; }

    public async Task<Result<int>> Handle(SaveRoomTypeCommand r, CancellationToken ct)
    {
        try
        {
            RoomType rt;
            if (r.Id > 0)
            {
                rt = await _repo.GetByIdAsync(r.Id, ct) ?? throw new InvalidOperationException("نوعِ اتاق یافت نشد.");
                rt.Update(r.Name, r.BaseCapacity, r.ExtraBedAllowed, r.Active);
            }
            else
            {
                rt = RoomType.Create(_user.CompanyId ?? 1, r.Code, r.Name, r.BaseCapacity, r.ExtraBedAllowed);
                await _repo.AddAsync(rt, ct);
            }
            await _uow.SaveChangesAsync(ct);
            return Result<int>.Success(rt.Id);
        }
        catch (Exception ex) { return Result<int>.Failure(ex.GetBaseException().Message); }
    }
}
