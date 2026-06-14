using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SamaHesab.Application.Accounting.Commands;
using SamaHesab.Application.Accounting.Queries;

namespace SamaHesab.API.Controllers;

[ApiController]
[Authorize(Roles = "ADMIN")]
[Route("api/[controller]")]
public class AccountsController : ControllerBase
{
    private readonly IMediator _mediator;
    public AccountsController(IMediator mediator) => _mediator = mediator;

    /// <summary>فهرستِ نمودار حساب‌ها (اختیاری: فقط حساب‌های برگ با leafOnly=true).</summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool leafOnly, CancellationToken ct)
        => Ok(await _mediator.Send(new GetAccountsQuery(leafOnly), ct));

    /// <summary>ایجاد/ویرایشِ حساب. Id=null → ایجاد؛ Id دارد → ویرایش.</summary>
    [HttpPost]
    public async Task<IActionResult> Save([FromBody] SaveAccountCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return result.Succeeded
            ? Ok(new { accountId = result.Value })
            : BadRequest(new { message = result.ErrorMessage });
    }

    /// <summary>حذفِ حساب (تنها اگر نه تراکنش دارد نه زیرحساب).</summary>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var result = await _mediator.Send(new DeleteAccountCommand(id), ct);
        return result.Succeeded
            ? Ok(new { deleted = true })
            : BadRequest(new { message = result.ErrorMessage });
    }
}
