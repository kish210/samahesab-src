using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SamaHesab.Modules.Hotel.Application;

namespace SamaHesab.API.Controllers;

/// <summary>
/// U-WEB-HOTEL — ماژولِ هتل/اقامتگاه (PMS). این ماژول تا این‌جا فقط لایهٔ Domain داشت (بدونِ
/// Application/API/UI)؛ CQRSِ نو در Modules.Hotel/Application اضافه شد (اتاق/نوعِ اتاق/رزرو/فولیو).
/// ⚠️ محدودیتِ صادقانه: RatePlan/Deposit/HousekeepingTask/MaintenanceTicket/NightAuditRun/
/// PmsSettings هنوز CQRS ندارند — این نسخه فقط چرخهٔ اصلیِ اتاق→رزرو→ورود→فولیو→خروج را پوشش می‌دهد.
/// </summary>
[ApiController]
[Authorize]
[Route("api/hotel")]
public class HotelController : ControllerBase
{
    private readonly IMediator _mediator;
    public HotelController(IMediator mediator) => _mediator = mediator;

    // ── نوعِ اتاق ──
    [HttpGet("room-types")]
    public async Task<IActionResult> RoomTypes([FromQuery] bool includeInactive, CancellationToken ct)
        => Ok(await _mediator.Send(new GetRoomTypesQuery(includeInactive), ct));

    [HttpPost("room-types")]
    public async Task<IActionResult> SaveRoomType([FromBody] SaveRoomTypeCommand cmd, CancellationToken ct)
    {
        var r = await _mediator.Send(cmd, ct);
        return r.Succeeded ? Ok(new { id = r.Value }) : BadRequest(new { message = r.ErrorMessage });
    }

    // ── اتاق ──
    [HttpGet("rooms")]
    public async Task<IActionResult> Rooms([FromQuery] bool includeInactive, CancellationToken ct)
        => Ok(await _mediator.Send(new GetRoomsQuery(includeInactive), ct));

    [HttpPost("rooms")]
    public async Task<IActionResult> SaveRoom([FromBody] SaveRoomCommand cmd, CancellationToken ct)
    {
        var r = await _mediator.Send(cmd, ct);
        return r.Succeeded ? Ok(new { id = r.Value }) : BadRequest(new { message = r.ErrorMessage });
    }

    [HttpPost("rooms/{id:int}/status")]
    public async Task<IActionResult> SetRoomStatus(int id, [FromBody] SetRoomStatusCommand cmd, CancellationToken ct)
    {
        var r = await _mediator.Send(cmd with { RoomId = id }, ct);
        return r.Succeeded ? Ok() : BadRequest(new { message = r.ErrorMessage });
    }

    // ── رزرو ──
    [HttpGet("reservations")]
    public async Task<IActionResult> Reservations([FromQuery] string? from, [FromQuery] string? to,
        [FromQuery] Modules.Hotel.Domain.ReservationStatus? status, CancellationToken ct)
        => Ok(await _mediator.Send(new GetReservationsQuery(from, to, status), ct));

    [HttpGet("reservations/{id:int}")]
    public async Task<IActionResult> Reservation(int id, CancellationToken ct)
    {
        var r = await _mediator.Send(new GetReservationQuery(id), ct);
        return r is null ? NotFound() : Ok(r);
    }

    [HttpPost("reservations")]
    public async Task<IActionResult> CreateReservation([FromBody] CreateReservationCommand cmd, CancellationToken ct)
    {
        var r = await _mediator.Send(cmd, ct);
        return r.Succeeded ? Ok(new { id = r.Value }) : BadRequest(new { message = r.ErrorMessage });
    }

    [HttpPost("reservations/{id:int}/check-in")]
    public async Task<IActionResult> CheckIn(int id, [FromBody] CheckInCommand cmd, CancellationToken ct)
    {
        var r = await _mediator.Send(cmd with { ReservationId = id }, ct);
        return r.Succeeded ? Ok(new { folioId = r.Value }) : BadRequest(new { message = r.ErrorMessage });
    }

    [HttpPost("reservations/{id:int}/check-out")]
    public async Task<IActionResult> CheckOut(int id, [FromBody] CheckOutCommand cmd, CancellationToken ct)
    {
        var r = await _mediator.Send(cmd with { ReservationId = id }, ct);
        return r.Succeeded ? Ok() : BadRequest(new { message = r.ErrorMessage });
    }

    [HttpPost("reservations/{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, CancellationToken ct)
    {
        var r = await _mediator.Send(new CancelReservationCommand(id), ct);
        return r.Succeeded ? Ok() : BadRequest(new { message = r.ErrorMessage });
    }

    // ── فولیو ──
    [HttpGet("reservations/{id:int}/folio")]
    public async Task<IActionResult> Folio(int id, CancellationToken ct)
    {
        var f = await _mediator.Send(new GetFolioByReservationQuery(id), ct);
        return f is null ? NotFound() : Ok(f);
    }

    [HttpPost("folios/{id:int}/charges")]
    public async Task<IActionResult> AddCharge(int id, [FromBody] AddFolioChargeCommand cmd, CancellationToken ct)
    {
        var r = await _mediator.Send(cmd with { FolioId = id }, ct);
        return r.Succeeded ? Ok() : BadRequest(new { message = r.ErrorMessage });
    }

    [HttpPost("folios/{id:int}/payments")]
    public async Task<IActionResult> AddPayment(int id, [FromBody] AddFolioPaymentCommand cmd, CancellationToken ct)
    {
        var r = await _mediator.Send(cmd with { FolioId = id }, ct);
        return r.Succeeded ? Ok() : BadRequest(new { message = r.ErrorMessage });
    }
}
