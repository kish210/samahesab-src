using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SamaHesab.Application.Accounting.Commands;

namespace SamaHesab.API.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
public class VouchersController : ControllerBase
{
    private readonly IMediator _mediator;
    public VouchersController(IMediator mediator) => _mediator = mediator;

    /// <summary>Create a (draft) accounting voucher.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateVoucherCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return result.Succeeded
            ? Ok(new { voucherId = result.Value })
            : BadRequest(new { message = result.ErrorMessage });
    }

    /// <summary>Post (finalize) a voucher.</summary>
    [HttpPost("{id:int}/post")]
    public async Task<IActionResult> Post(int id, CancellationToken ct)
    {
        var result = await _mediator.Send(new PostVoucherCommand(id), ct);
        return result.Succeeded
            ? Ok(new { posted = true })
            : BadRequest(new { message = result.ErrorMessage });
    }
}
