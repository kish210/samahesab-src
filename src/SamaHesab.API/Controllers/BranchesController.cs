using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SamaHesab.Application.Settings.Commands;

namespace SamaHesab.API.Controllers;

/// <summary>مدیریت شعب (چندشعبه — هستهٔ ERP).</summary>
[ApiController]
[Route("api/branches")]
[Authorize]
public class BranchesController : ControllerBase
{
    private readonly IMediator _mediator;
    public BranchesController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> List() => Ok(await _mediator.Send(new GetBranchesQuery()));

    [HttpPost]
    public async Task<IActionResult> Save([FromBody] SaveBranchCommand cmd)
    {
        var r = await _mediator.Send(cmd);
        return r.Succeeded ? Ok(new { id = r.Value }) : BadRequest(new { message = r.ErrorMessage });
    }

    [HttpPut("{id:int}/active/{active:bool}")]
    public async Task<IActionResult> Toggle(int id, bool active)
    {
        var r = await _mediator.Send(new ToggleBranchCommand(id, active));
        return r.Succeeded ? Ok() : BadRequest(new { message = r.ErrorMessage });
    }
}
