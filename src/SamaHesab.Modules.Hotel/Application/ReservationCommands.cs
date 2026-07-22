using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Entities.CRM;
using SamaHesab.Domain.Interfaces.Repositories;
using SamaHesab.Modules.Hotel.Domain;

namespace SamaHesab.Modules.Hotel.Application;

// ── فهرست/جزئیات ──────────────────────────────────────────────────────────────
public record ReservationRoomDto(int Id, int RoomTypeId, string RoomTypeName, int? RoomId, string? RoomNumber, decimal RatePerNight, int ExtraBeds);

public record ReservationDto(int Id, string GuestName, string CheckInDate, string CheckOutDate, int Nights,
    int Adults, int Children, ReservationStatus Status, ReservationSource Source, string? Notes,
    List<ReservationRoomDto> Rooms);

public record GetReservationsQuery(string? FromDate = null, string? ToDate = null, ReservationStatus? Status = null)
    : IRequest<List<ReservationDto>>;

public class GetReservationsQueryHandler : IRequestHandler<GetReservationsQuery, List<ReservationDto>>
{
    private readonly IReservationRepository _reservations;
    private readonly IRepository<Party> _parties;
    private readonly IRepository<RoomType> _types;
    private readonly IRepository<Room> _rooms;
    private readonly ICurrentUserService _user;

    public GetReservationsQueryHandler(IReservationRepository reservations, IRepository<Party> parties,
        IRepository<RoomType> types, IRepository<Room> rooms, ICurrentUserService user)
    { _reservations = reservations; _parties = parties; _types = types; _rooms = rooms; _user = user; }

    public async Task<List<ReservationDto>> Handle(GetReservationsQuery req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        var all = await _reservations.FindWithRoomsAsync(r => r.CompanyId == companyId, ct);
        var filtered = all
            .Where(r => req.Status == null || r.Status == req.Status)
            .Where(r => req.FromDate == null || string.CompareOrdinal(r.CheckOutDate, req.FromDate) >= 0)
            .Where(r => req.ToDate == null || string.CompareOrdinal(r.CheckInDate, req.ToDate) <= 0)
            .OrderByDescending(r => r.CheckInDate)
            .ToList();

        var guestIds = filtered.Select(r => r.GuestPartyId).Distinct().ToList();
        var guests = (await _parties.FindAsync(p => guestIds.Contains(p.Id), ct)).ToDictionary(p => p.Id, p => p.FullName);
        var typeNames = (await _types.FindAsync(t => t.CompanyId == companyId, ct)).ToDictionary(t => t.Id, t => t.Name);
        var roomNumbers = (await _rooms.FindAsync(rm => rm.CompanyId == companyId, ct)).ToDictionary(rm => rm.Id, rm => rm.Number);

        return filtered.Select(r => Map(r, guests, typeNames, roomNumbers)).ToList();
    }

    internal static ReservationDto Map(Reservation r, Dictionary<int, string> guests,
        Dictionary<int, string> typeNames, Dictionary<int, string> roomNumbers) => new(
        r.Id, guests.GetValueOrDefault(r.GuestPartyId, "—"), r.CheckInDate, r.CheckOutDate, r.Nights,
        r.Adults, r.Children, r.Status, r.Source, r.Notes,
        r.Rooms.Select(rr => new ReservationRoomDto(rr.Id, rr.RoomTypeId, typeNames.GetValueOrDefault(rr.RoomTypeId, "—"),
            rr.RoomId, rr.RoomId.HasValue ? roomNumbers.GetValueOrDefault(rr.RoomId.Value, "—") : null, rr.RatePerNight, rr.ExtraBeds)).ToList());
}

public record GetReservationQuery(int Id) : IRequest<ReservationDto?>;

public class GetReservationQueryHandler : IRequestHandler<GetReservationQuery, ReservationDto?>
{
    private readonly IReservationRepository _reservations;
    private readonly IRepository<Party> _parties;
    private readonly IRepository<RoomType> _types;
    private readonly IRepository<Room> _rooms;
    private readonly ICurrentUserService _user;

    public GetReservationQueryHandler(IReservationRepository reservations, IRepository<Party> parties,
        IRepository<RoomType> types, IRepository<Room> rooms, ICurrentUserService user)
    { _reservations = reservations; _parties = parties; _types = types; _rooms = rooms; _user = user; }

    public async Task<ReservationDto?> Handle(GetReservationQuery req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        var r = (await _reservations.FindWithRoomsAsync(x => x.Id == req.Id && x.CompanyId == companyId, ct)).FirstOrDefault();
        if (r is null) return null;
        var guest = await _parties.GetByIdAsync(r.GuestPartyId, ct);
        var typeNames = (await _types.FindAsync(t => t.CompanyId == companyId, ct)).ToDictionary(t => t.Id, t => t.Name);
        var roomNumbers = (await _rooms.FindAsync(rm => rm.CompanyId == companyId, ct)).ToDictionary(rm => rm.Id, rm => rm.Number);
        var guests = new Dictionary<int, string> { [r.GuestPartyId] = guest?.FullName ?? "—" };
        return GetReservationsQueryHandler.Map(r, guests, typeNames, roomNumbers);
    }
}

// ── ساختِ رزرو ────────────────────────────────────────────────────────────────
public record ReservationRoomLine(int RoomTypeId, decimal RatePerNight, int ExtraBeds = 0);

public record CreateReservationCommand(
    int GuestPartyId, ReservationSource Source, string CheckInDate, string CheckOutDate, int Nights,
    int Adults, int Children, List<ReservationRoomLine> Rooms, string? Notes = null) : IRequest<Result<int>>;

public class CreateReservationCommandHandler : IRequestHandler<CreateReservationCommand, Result<int>>
{
    private readonly IReservationRepository _repo;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;
    public CreateReservationCommandHandler(IReservationRepository repo, IUnitOfWork uow, ICurrentUserService user)
    { _repo = repo; _uow = uow; _user = user; }

    public async Task<Result<int>> Handle(CreateReservationCommand r, CancellationToken ct)
    {
        try
        {
            if (r.Rooms.Count == 0) return Result<int>.Failure("دستِ‌کم یک اتاق باید انتخاب شود.");
            var res = Reservation.Create(_user.CompanyId ?? 1, _user.BranchId ?? 1, r.GuestPartyId, r.Source,
                r.CheckInDate, r.CheckOutDate, r.Nights, r.Adults, r.Children, notes: r.Notes);
            foreach (var line in r.Rooms)
                res.AddRoom(ReservationRoom.Create(line.RoomTypeId, line.RatePerNight, extraBeds: line.ExtraBeds));
            await _repo.AddAsync(res, ct);
            await _uow.SaveChangesAsync(ct);
            return Result<int>.Success(res.Id);
        }
        catch (Exception ex) { return Result<int>.Failure(ex.GetBaseException().Message); }
    }
}

// ── چرخهٔ اقامت ───────────────────────────────────────────────────────────────
public record RoomAssignment(int ReservationRoomId, int RoomId);
public record CheckInCommand(int ReservationId, List<RoomAssignment> Assignments, string Date) : IRequest<Result<int>>;

public class CheckInCommandHandler : IRequestHandler<CheckInCommand, Result<int>>
{
    private readonly IReservationRepository _reservations;
    private readonly IRepository<Room> _rooms;
    private readonly IFolioRepository _folios;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;

    public CheckInCommandHandler(IReservationRepository reservations, IRepository<Room> rooms,
        IFolioRepository folios, IUnitOfWork uow, ICurrentUserService user)
    { _reservations = reservations; _rooms = rooms; _folios = folios; _uow = uow; _user = user; }

    public async Task<Result<int>> Handle(CheckInCommand r, CancellationToken ct)
    {
        try
        {
            var res = await _reservations.GetWithRoomsAsync(r.ReservationId, ct)
                ?? throw new InvalidOperationException("رزرو یافت نشد.");
            if (res.Status is ReservationStatus.CheckedIn or ReservationStatus.CheckedOut or ReservationStatus.Cancelled)
                return Result<int>.Failure("این رزرو در وضعیتِ قابلِ ورود نیست.");

            foreach (var a in r.Assignments)
            {
                var line = res.Rooms.FirstOrDefault(x => x.Id == a.ReservationRoomId)
                    ?? throw new InvalidOperationException("خطِ اتاقِ رزرو یافت نشد.");
                line.AssignRoom(a.RoomId);
                var room = await _rooms.GetByIdAsync(a.RoomId, ct) ?? throw new InvalidOperationException("اتاقِ فیزیکی یافت نشد.");
                room.SetStatus(RoomStatus.Occupied_Dirty);
            }
            res.SetStatus(ReservationStatus.CheckedIn);

            var folio = Folio.Create(_user.CompanyId ?? 1, res.Id, r.Date, res.Rooms.FirstOrDefault()?.RoomId);
            await _folios.AddAsync(folio, ct);
            await _uow.SaveChangesAsync(ct);
            return Result<int>.Success(folio.Id);
        }
        catch (Exception ex) { return Result<int>.Failure(ex.GetBaseException().Message); }
    }
}

public record CheckOutCommand(int ReservationId, string Date) : IRequest<Result>;

public class CheckOutCommandHandler : IRequestHandler<CheckOutCommand, Result>
{
    private readonly IReservationRepository _reservations;
    private readonly IRepository<Room> _rooms;
    private readonly IFolioRepository _folios;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;

    public CheckOutCommandHandler(IReservationRepository reservations, IRepository<Room> rooms,
        IFolioRepository folios, IUnitOfWork uow, ICurrentUserService user)
    { _reservations = reservations; _rooms = rooms; _folios = folios; _uow = uow; _user = user; }

    public async Task<Result> Handle(CheckOutCommand r, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        var res = await _reservations.GetWithRoomsAsync(r.ReservationId, ct);
        if (res is null) return Result.Failure("رزرو یافت نشد.");
        if (res.Status != ReservationStatus.CheckedIn) return Result.Failure("این رزرو در حالِ اقامت نیست.");

        var folio = await _folios.FindSingleAsync(f => f.ReservationId == res.Id && f.CompanyId == companyId, ct);
        if (folio is not null && folio.Balance > 0)
            return Result.Failure($"ماندهٔ فولیو تسویه نشده ({folio.Balance:N0} ریال) — ابتدا پرداخت را ثبت کنید.");

        foreach (var line in res.Rooms.Where(l => l.RoomId.HasValue))
        {
            var room = await _rooms.GetByIdAsync(line.RoomId!.Value, ct);
            room?.SetStatus(RoomStatus.Vacant_Dirty);
        }
        res.SetStatus(ReservationStatus.CheckedOut);
        folio?.Close(r.Date);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}

public record CancelReservationCommand(int ReservationId) : IRequest<Result>;

public class CancelReservationCommandHandler : IRequestHandler<CancelReservationCommand, Result>
{
    private readonly IReservationRepository _repo;
    private readonly IUnitOfWork _uow;
    public CancelReservationCommandHandler(IReservationRepository repo, IUnitOfWork uow) { _repo = repo; _uow = uow; }

    public async Task<Result> Handle(CancelReservationCommand r, CancellationToken ct)
    {
        var res = await _repo.GetByIdAsync(r.ReservationId, ct);
        if (res is null) return Result.Failure("رزرو یافت نشد.");
        if (res.Status is ReservationStatus.CheckedIn or ReservationStatus.CheckedOut)
            return Result.Failure("رزروِ در حالِ اقامت/تسویه‌شده را نمی‌توان لغو کرد.");
        res.SetStatus(ReservationStatus.Cancelled);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
