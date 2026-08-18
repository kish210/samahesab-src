using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SamaHesab.Application.Accounting.Commands;
using SamaHesab.Application.Accounting.Queries;

namespace SamaHesab.API.Controllers;

/// <summary>
/// U-FIXED-ASSET — داراییِ ثابت و استهلاک (هم‌راستا با «نرم‌افزار دارایی ثابتِ» راهکاران).
/// </summary>
[ApiController]
[Authorize]
[Route("api/fixed-assets")]
public class FixedAssetsController : ControllerBase
{
    private readonly IMediator _mediator;
    public FixedAssetsController(IMediator mediator) => _mediator = mediator;

    /// <summary>فهرستِ دارایی‌ها با ارزشِ دفتری/استهلاکِ ماهانهٔ محاسبه‌شده.</summary>
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
        => Ok(await _mediator.Send(new GetFixedAssetsQuery(), ct));

    /// <summary>ساختِ داراییِ ثابت.</summary>
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateFixedAssetCommand cmd, CancellationToken ct)
    {
        var r = await _mediator.Send(cmd, ct);
        return r.Succeeded ? Ok(new { id = r.Value }) : BadRequest(new { message = r.ErrorMessage });
    }

    /// <summary>اجرایِ استهلاکِ دوره («yyyy/MM») و صدورِ سندِ تجمیعی.</summary>
    [HttpPost("depreciate")]
    public async Task<IActionResult> Depreciate([FromBody] DepreciateFixedAssetsCommand cmd, CancellationToken ct)
    {
        var r = await _mediator.Send(cmd, ct);
        return r.Succeeded ? Ok(new { voucherId = r.Value }) : BadRequest(new { message = r.ErrorMessage });
    }

    /// <summary>ویرایشِ دارایی (Idِ مسیر مرجع است).</summary>
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateFixedAssetCommand cmd, CancellationToken ct)
    {
        var r = await _mediator.Send(cmd with { Id = id }, ct);
        return r.Succeeded ? Ok(new { id }) : BadRequest(new { message = r.ErrorMessage });
    }

    /// <summary>غیرفعال‌سازیِ دارایی (حذفِ سخت عمداً نیست).</summary>
    [HttpPost("{id:int}/deactivate")]
    public async Task<IActionResult> Deactivate(int id, CancellationToken ct)
    {
        var r = await _mediator.Send(new SetFixedAssetActiveCommand(id, false), ct);
        return r.Succeeded ? Ok() : BadRequest(new { message = r.ErrorMessage });
    }

    /// <summary>بازفعال‌سازیِ دارایی.</summary>
    [HttpPost("{id:int}/activate")]
    public async Task<IActionResult> Activate(int id, CancellationToken ct)
    {
        var r = await _mediator.Send(new SetFixedAssetActiveCommand(id, true), ct);
        return r.Succeeded ? Ok() : BadRequest(new { message = r.ErrorMessage });
    }
}
