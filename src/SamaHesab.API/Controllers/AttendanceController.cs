using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SamaHesab.Application.HRM;

namespace SamaHesab.API.Controllers;

/// <summary>
/// U-WEB-ATTENDANCE — ماژولِ حضوروغیاب. لایهٔ Application از قبل کامل بود (تردد/مرخصی/تجمیعِ
/// ماهانه) ولی هیچ endpointی صدایش نمی‌زد. ⚠️ محدودیتِ صادقانه: واردکردنِ ضربه از دستگاه
/// (AttendanceImport)، شیفت/تقویمِ تعطیلات (Shift/HolidayCommands) و کاردکسِ مرخصی
/// (GetLeaveKardexQuery) هنوز endpoint ندارند — این پورت فقط برگهٔ روزانه/تجمیعِ ماهانه/
/// درخواستِ مرخصی را پوشش می‌دهد (پرکاربردترین بخشِ روزمرهٔ این ماژول).
/// </summary>
[ApiController]
[Authorize]
[Route("api/attendance")]
public class AttendanceController : ControllerBase
{
    private readonly IMediator _mediator;
    public AttendanceController(IMediator mediator) => _mediator = mediator;

    [HttpGet("day")]
    public async Task<IActionResult> Day([FromQuery] string workDate, CancellationToken ct)
        => Ok(await _mediator.Send(new GetAttendanceQuery(workDate), ct));

    [HttpPost("upsert")]
    public async Task<IActionResult> Upsert([FromBody] UpsertAttendanceCommand cmd, CancellationToken ct)
    {
        var r = await _mediator.Send(cmd, ct);
        return r.Succeeded ? Ok(new { id = r.Value }) : BadRequest(new { message = r.ErrorMessage });
    }

    [HttpPost("mark-batch")]
    public async Task<IActionResult> MarkBatch([FromBody] MarkBatchAttendanceCommand cmd, CancellationToken ct)
    {
        var r = await _mediator.Send(cmd, ct);
        return r.Succeeded ? Ok(new { count = r.Value }) : BadRequest(new { message = r.ErrorMessage });
    }

    [HttpGet("monthly")]
    public async Task<IActionResult> Monthly([FromQuery] int employeeId, [FromQuery] string year,
        [FromQuery] byte month, CancellationToken ct)
        => Ok(await _mediator.Send(new GetMonthlyAttendanceQuery(employeeId, year, month), ct));

    [HttpGet("leaves")]
    public async Task<IActionResult> Leaves([FromQuery] bool pendingOnly, CancellationToken ct)
        => Ok(await _mediator.Send(new GetLeaveRequestsQuery(pendingOnly), ct));

    [HttpPost("leaves")]
    public async Task<IActionResult> RequestLeave([FromBody] RequestLeaveCommand cmd, CancellationToken ct)
    {
        var r = await _mediator.Send(cmd, ct);
        return r.Succeeded ? Ok(new { id = r.Value }) : BadRequest(new { message = r.ErrorMessage });
    }

    [HttpPost("leaves/{id:int}/decide")]
    public async Task<IActionResult> DecideLeave(int id, [FromBody] DecideLeaveCommand cmd, CancellationToken ct)
    {
        var r = await _mediator.Send(cmd with { LeaveRequestId = id }, ct);
        return r.Succeeded ? Ok() : BadRequest(new { message = r.ErrorMessage });
    }

    // ── دستگاه‌هایِ تردد (زدکتکو) ──
    [HttpGet("devices")]
    public async Task<IActionResult> Devices([FromQuery] bool activeOnly, CancellationToken ct)
        => Ok(await _mediator.Send(new GetDevicesQuery(activeOnly), ct));

    [HttpPost("devices")]
    public async Task<IActionResult> SaveDevice([FromBody] SaveDeviceCommand cmd, CancellationToken ct)
    {
        var r = await _mediator.Send(cmd, ct);
        return r.Succeeded ? Ok(new { id = r.Value }) : BadRequest(new { message = r.ErrorMessage });
    }

    [HttpPost("devices/{id:int}/sync")]
    public async Task<IActionResult> SyncDevice(int id, CancellationToken ct)
    {
        var r = await _mediator.Send(new SyncDeviceAttendanceCommand(id), ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { message = r.ErrorMessage });
    }
}
