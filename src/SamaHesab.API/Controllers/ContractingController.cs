using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SamaHesab.Application.Accounting.Dimensions;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Modules.Contracting.Application;
using SamaHesab.Modules.Contracting.Application.Commands;
using SamaHesab.Modules.Contracting.Application.Queries;
using SamaHesab.Modules.Contracting.Domain;

namespace SamaHesab.API.Controllers;

/// <summary>
/// U-WEB-CONTRACTING — پیمانکاری (پیمان/صورت‌وضعیت/پیش‌پرداخت/ضمانت‌نامه/تنظیمات).
/// BranchId/FiscalYearId برایِ فرمان‌هایِ حسابداری‌زا سمتِ سرور حل می‌شود (طبقِ الگویِ رفعِ
/// هاردکدِ FiscalYearId این نشست) تا کلاینتِ وب مجبور به دانستنِ آن‌ها نباشد.
/// ⚠️ محدودیتِ صادقانه: قراردادهایِ خدماتیِ تکرارشونده (ServiceContract) در این پورت نیامده —
/// عمداً خارج از حدود (پیمان/صورت‌وضعیت چرخهٔ اصلیِ این ماژول است).
/// </summary>
[ApiController]
[Authorize]
[Route("api/contracting")]
public class ContractingController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;
    public ContractingController(IMediator mediator, ICurrentUserService currentUser)
    { _mediator = mediator; _currentUser = currentUser; }

    // ── پیمان‌ها ──
    [HttpGet("projects")]
    public async Task<IActionResult> Projects([FromQuery] bool activeOnly, CancellationToken ct)
        => Ok(await _mediator.Send(new GetContractProjectsQuery(activeOnly), ct));

    [HttpPost("projects")]
    public async Task<IActionResult> SaveProject([FromBody] SaveContractProjectCommand cmd, CancellationToken ct)
    {
        var r = await _mediator.Send(cmd, ct);
        return r.Succeeded ? Ok(new { id = r.Value }) : BadRequest(new { message = r.ErrorMessage });
    }

    [HttpGet("projects/{id:int}/dashboard")]
    public async Task<IActionResult> Dashboard(int id, CancellationToken ct)
    {
        var dto = await _mediator.Send(new GetProjectDashboardQuery(id), ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    // ── پیش‌پرداخت ──
    [HttpGet("projects/{id:int}/advances")]
    public async Task<IActionResult> Advances(int id, CancellationToken ct)
        => Ok(await _mediator.Send(new GetAdvancePaymentsQuery(id), ct));

    public record ReceiveAdvanceRequest(int ContractProjectId, string Date, decimal Amount, string PaymentMethod = "بانک", string? Note = null);

    [HttpPost("advances/receive")]
    public async Task<IActionResult> ReceiveAdvance([FromBody] ReceiveAdvanceRequest req, CancellationToken ct)
    {
        var fiscalYearId = await _mediator.Send(new GetActiveFiscalYearQuery(), ct);
        var cmd = new ReceiveAdvanceCommand(_currentUser.BranchId ?? 1, fiscalYearId, req.Date,
            req.ContractProjectId, req.Amount, req.PaymentMethod, req.Note);
        var r = await _mediator.Send(cmd, ct);
        return r.Succeeded ? Ok(new { id = r.Value }) : BadRequest(new { message = r.ErrorMessage });
    }

    public record ReleaseDepositRequest(DeductionType DepositType, decimal Amount, string Date, string? Note = null);

    [HttpPost("deposits/release")]
    public async Task<IActionResult> ReleaseDeposit([FromBody] ReleaseDepositRequest req, CancellationToken ct)
    {
        var fiscalYearId = await _mediator.Send(new GetActiveFiscalYearQuery(), ct);
        var cmd = new ReleaseDepositCommand(_currentUser.BranchId ?? 1, fiscalYearId, req.Date,
            req.DepositType, req.Amount, req.Note);
        var r = await _mediator.Send(cmd, ct);
        return r.Succeeded ? Ok(new { voucherId = r.Value }) : BadRequest(new { message = r.ErrorMessage });
    }

    // ── ضمانت‌نامه ──
    [HttpGet("guarantees")]
    public async Task<IActionResult> Guarantees([FromQuery] int? projectId, [FromQuery] bool activeOnly, CancellationToken ct)
        => Ok(await _mediator.Send(new GetGuaranteesQuery(projectId, activeOnly), ct));

    [HttpPost("guarantees")]
    public async Task<IActionResult> RegisterGuarantee([FromBody] RegisterGuaranteeCommand cmd, CancellationToken ct)
    {
        var r = await _mediator.Send(cmd, ct);
        return r.Succeeded ? Ok(new { id = r.Value }) : BadRequest(new { message = r.ErrorMessage });
    }

    [HttpPost("guarantees/{id:int}/release")]
    public async Task<IActionResult> ReleaseGuarantee(int id, CancellationToken ct)
    {
        var r = await _mediator.Send(new ReleaseGuaranteeCommand(id), ct);
        return r.Succeeded ? Ok() : BadRequest(new { message = r.ErrorMessage });
    }

    // ── صورت‌وضعیت ──
    [HttpGet("projects/{id:int}/statements")]
    public async Task<IActionResult> Statements(int id, CancellationToken ct)
        => Ok(await _mediator.Send(new GetProgressStatementsQuery(id), ct));

    [HttpPost("statements")]
    public async Task<IActionResult> SaveStatement([FromBody] SaveProgressStatementCommand cmd, CancellationToken ct)
    {
        var r = await _mediator.Send(cmd, ct);
        return r.Succeeded ? Ok(new { id = r.Value }) : BadRequest(new { message = r.ErrorMessage });
    }

    [HttpPost("statements/{id:int}/post")]
    public async Task<IActionResult> PostStatement(int id, CancellationToken ct)
    {
        var fiscalYearId = await _mediator.Send(new GetActiveFiscalYearQuery(), ct);
        var r = await _mediator.Send(new PostProgressStatementCommand(id, _currentUser.BranchId ?? 1, fiscalYearId), ct);
        return r.Succeeded ? Ok(new { voucherId = r.Value }) : BadRequest(new { message = r.ErrorMessage });
    }

    // ── تنظیمات ──
    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings(CancellationToken ct)
        => Ok(await _mediator.Send(new GetContractingSettingsQuery(), ct));

    [HttpPost("settings")]
    public async Task<IActionResult> SaveSettings([FromBody] ContractingSettingsDto dto, CancellationToken ct)
    {
        var r = await _mediator.Send(new SaveContractingSettingsCommand(dto), ct);
        return r.Succeeded ? Ok(new { id = r.Value }) : BadRequest(new { message = r.ErrorMessage });
    }
}
