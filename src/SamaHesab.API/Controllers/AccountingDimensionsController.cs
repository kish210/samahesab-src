using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SamaHesab.Application.Accounting.Dimensions;

namespace SamaHesab.API.Controllers;

/// <summary>ابعاد حسابداری (هستهٔ ERP): سال مالی · مرکز هزینه · پروژه.</summary>
[ApiController]
[Route("api/accounting/dimensions")]
[Authorize]
public class AccountingDimensionsController : ControllerBase
{
    private readonly IMediator _mediator;
    public AccountingDimensionsController(IMediator mediator) => _mediator = mediator;

    // ── سال مالی ──
    [HttpGet("fiscal-years")]
    public async Task<IActionResult> FiscalYears() => Ok(await _mediator.Send(new GetFiscalYearsQuery()));

    [HttpPost("fiscal-years")]
    public async Task<IActionResult> SaveFiscalYear([FromBody] SaveFiscalYearCommand cmd)
    {
        var r = await _mediator.Send(cmd);
        return r.Succeeded ? Ok(new { id = r.Value }) : BadRequest(new { message = r.ErrorMessage });
    }

    [HttpPut("fiscal-years/{id:int}/closed/{closed:bool}")]
    public async Task<IActionResult> SetFiscalYearClosed(int id, bool closed)
    {
        var r = await _mediator.Send(new SetFiscalYearClosedCommand(id, closed));
        return r.Succeeded ? Ok() : BadRequest(new { message = r.ErrorMessage });
    }

    // ── مرکز هزینه ──
    [HttpGet("cost-centers")]
    public async Task<IActionResult> CostCenters([FromQuery] bool activeOnly = false)
        => Ok(await _mediator.Send(new GetCostCentersQuery(activeOnly)));

    [HttpPost("cost-centers")]
    public async Task<IActionResult> SaveCostCenter([FromBody] SaveCostCenterCommand cmd)
    {
        var r = await _mediator.Send(cmd);
        return r.Succeeded ? Ok(new { id = r.Value }) : BadRequest(new { message = r.ErrorMessage });
    }

    [HttpPut("cost-centers/{id:int}/active/{active:bool}")]
    public async Task<IActionResult> SetCostCenterActive(int id, bool active)
    {
        var r = await _mediator.Send(new SetCostCenterActiveCommand(id, active));
        return r.Succeeded ? Ok() : BadRequest(new { message = r.ErrorMessage });
    }

    // ── پروژه ──
    [HttpGet("projects")]
    public async Task<IActionResult> Projects([FromQuery] bool activeOnly = false)
        => Ok(await _mediator.Send(new GetProjectsQuery(activeOnly)));

    [HttpPost("projects")]
    public async Task<IActionResult> SaveProject([FromBody] SaveProjectCommand cmd)
    {
        var r = await _mediator.Send(cmd);
        return r.Succeeded ? Ok(new { id = r.Value }) : BadRequest(new { message = r.ErrorMessage });
    }

    [HttpPut("projects/{id:int}/closed/{closed:bool}")]
    public async Task<IActionResult> SetProjectClosed(int id, bool closed)
    {
        var r = await _mediator.Send(new SetProjectClosedCommand(id, closed));
        return r.Succeeded ? Ok() : BadRequest(new { message = r.ErrorMessage });
    }
}
