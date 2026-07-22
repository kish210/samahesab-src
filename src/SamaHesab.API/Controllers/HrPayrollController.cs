using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SamaHesab.Application.HRM;

namespace SamaHesab.API.Controllers;

/// <summary>
/// U-WEB-HR — حقوق و دستمزد (ماژولِ اختیاریِ HR). CQRSِ کاملی از قبل در
/// SamaHesab.Modules.HR/Application/*.cs بود (محاسبه/اجرای دسته‌ای/صدورِ سند/تنظیمات/خروجی)
/// ولی هیچ endpointی صدایش نمی‌زد — کلاینتِ وب هیچ صفحهٔ حقوقی نداشت.
/// </summary>
[ApiController]
[Authorize]
[Route("api/hr/payroll")]
public class HrPayrollController : ControllerBase
{
    private readonly IMediator _mediator;
    public HrPayrollController(IMediator mediator) => _mediator = mediator;

    [HttpGet("slips")]
    public async Task<IActionResult> Slips([FromQuery] string year, [FromQuery] int month, CancellationToken ct)
        => Ok(await _mediator.Send(new GetSalarySlipsQuery(year, month), ct));

    [HttpPost("run")]
    public async Task<IActionResult> Run([FromBody] RunMonthlyPayrollCommand cmd, CancellationToken ct)
    {
        var r = await _mediator.Send(cmd, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { message = r.ErrorMessage });
    }

    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings([FromQuery] string year, CancellationToken ct)
        => Ok(await _mediator.Send(new GetPayrollSettingsQuery(year), ct));

    [HttpPut("settings")]
    public async Task<IActionResult> SaveSettings([FromBody] PayrollSettingsDto dto, CancellationToken ct)
    {
        var r = await _mediator.Send(new SavePayrollSettingsCommand(dto), ct);
        return r.Succeeded ? Ok(new { id = r.Value }) : BadRequest(new { message = r.ErrorMessage });
    }

    [HttpPost("post-voucher")]
    public async Task<IActionResult> PostVoucher([FromBody] PostSalaryVoucherCommand cmd, CancellationToken ct)
    {
        var r = await _mediator.Send(cmd, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { message = r.ErrorMessage });
    }

    [HttpGet("export")]
    public async Task<IActionResult> Export([FromQuery] string year, [FromQuery] int month,
        [FromQuery] string workshopCode = "", [FromQuery] string employerName = "", CancellationToken ct = default)
        => Ok(await _mediator.Send(new GetPayrollExportQuery(year, (byte)month, workshopCode, employerName), ct));
}
