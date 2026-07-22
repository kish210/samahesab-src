using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Interfaces.Repositories;
using SamaHesab.Modules.Hotel.Domain;

namespace SamaHesab.Modules.Hotel.Application;

/// <summary>U-WEB-HOTEL — اتاقِ فیزیکی + تابلوی وضعیت.</summary>
public record RoomDto(int Id, int RoomTypeId, string RoomTypeName, string Number, string? Floor, RoomStatus Status, bool Active);

public record GetRoomsQuery(bool IncludeInactive = false) : IRequest<List<RoomDto>>;

public class GetRoomsQueryHandler : IRequestHandler<GetRoomsQuery, List<RoomDto>>
{
    private readonly IRepository<Room> _rooms;
    private readonly IRepository<RoomType> _types;
    private readonly ICurrentUserService _user;
    public GetRoomsQueryHandler(IRepository<Room> rooms, IRepository<RoomType> types, ICurrentUserService user)
    { _rooms = rooms; _types = types; _user = user; }

    public async Task<List<RoomDto>> Handle(GetRoomsQuery req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        var all = await _rooms.FindAsync(r => r.CompanyId == companyId && (req.IncludeInactive || r.Active), ct);
        var types = (await _types.FindAsync(t => t.CompanyId == companyId, ct)).ToDictionary(t => t.Id, t => t.Name);
        return all.OrderBy(r => r.Number)
            .Select(r => new RoomDto(r.Id, r.RoomTypeId, types.GetValueOrDefault(r.RoomTypeId, "—"), r.Number, r.Floor, r.Status, r.Active))
            .ToList();
    }
}

public record SaveRoomCommand(int Id, int RoomTypeId, string Number, string? Floor, bool Active) : IRequest<Result<int>>;

public class SaveRoomCommandHandler : IRequestHandler<SaveRoomCommand, Result<int>>
{
    private readonly IRepository<Room> _repo;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;
    public SaveRoomCommandHandler(IRepository<Room> repo, IUnitOfWork uow, ICurrentUserService user)
    { _repo = repo; _uow = uow; _user = user; }

    public async Task<Result<int>> Handle(SaveRoomCommand r, CancellationToken ct)
    {
        try
        {
            Room room;
            if (r.Id > 0)
            {
                room = await _repo.GetByIdAsync(r.Id, ct) ?? throw new InvalidOperationException("اتاق یافت نشد.");
                room.Update(r.RoomTypeId, r.Number, r.Floor, r.Active);
            }
            else
            {
                room = Room.Create(_user.CompanyId ?? 1, r.RoomTypeId, r.Number, r.Floor);
                await _repo.AddAsync(room, ct);
            }
            await _uow.SaveChangesAsync(ct);
            return Result<int>.Success(room.Id);
        }
        catch (Exception ex) { return Result<int>.Failure(ex.GetBaseException().Message); }
    }
}

public record SetRoomStatusCommand(int RoomId, RoomStatus Status) : IRequest<Result>;

public class SetRoomStatusCommandHandler : IRequestHandler<SetRoomStatusCommand, Result>
{
    private readonly IRepository<Room> _repo;
    private readonly IUnitOfWork _uow;
    public SetRoomStatusCommandHandler(IRepository<Room> repo, IUnitOfWork uow) { _repo = repo; _uow = uow; }

    public async Task<Result> Handle(SetRoomStatusCommand r, CancellationToken ct)
    {
        var room = await _repo.GetByIdAsync(r.RoomId, ct);
        if (room is null) return Result.Failure("اتاق یافت نشد.");
        room.SetStatus(r.Status);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
