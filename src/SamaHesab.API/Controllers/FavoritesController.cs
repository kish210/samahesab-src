using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SamaHesab.Application.Common.Favorites;

namespace SamaHesab.API.Controllers;

/// <summary>
/// سرویس عمومی بهره‌وری: آیتم‌های «اخیر» و «سنجاق‌شده»ی هر کاربر برای هر نوع
/// (مشتری/کالا/حساب/سند …). در همه‌ی ماژول‌ها برای دسترسی سریع استفاده می‌شود.
/// </summary>
[ApiController]
[Authorize]
[Route("api/[controller]")]
public class FavoritesController : ControllerBase
{
    private readonly IMediator _mediator;
    public FavoritesController(IMediator mediator) => _mediator = mediator;

    /// <summary>اخیرترین آیتم‌های یک نوع (مثلاً Customer، Product).</summary>
    [HttpGet("recent/{entityType}")]
    public async Task<IActionResult> Recent(string entityType, [FromQuery] int top, CancellationToken ct)
        => Ok(await _mediator.Send(new GetRecentItemsQuery(entityType, top <= 0 ? 10 : top), ct));

    /// <summary>آیتم‌های سنجاق‌شده‌ی یک نوع.</summary>
    [HttpGet("pinned/{entityType}")]
    public async Task<IActionResult> Pinned(string entityType, CancellationToken ct)
        => Ok(await _mediator.Send(new GetPinnedItemsQuery(entityType), ct));

    public record TouchRequest(string EntityType, int EntityId, string Label);

    /// <summary>ثبت استفاده از یک آیتم (به‌روزرسانی فهرست اخیر).</summary>
    [HttpPost("touch")]
    public async Task<IActionResult> Touch([FromBody] TouchRequest req, CancellationToken ct)
    {
        var r = await _mediator.Send(new TouchRecentItemCommand(req.EntityType, req.EntityId, req.Label), ct);
        return r.Succeeded ? Ok() : BadRequest(new { message = r.ErrorMessage });
    }

    public record PinRequest(string EntityType, int EntityId, string Label, bool Pinned);

    /// <summary>سنجاق‌کردن/برداشتن سنجاقِ یک آیتم.</summary>
    [HttpPost("pin")]
    public async Task<IActionResult> Pin([FromBody] PinRequest req, CancellationToken ct)
    {
        var r = await _mediator.Send(new SetPinnedItemCommand(req.EntityType, req.EntityId, req.Label, req.Pinned), ct);
        return r.Succeeded ? Ok() : BadRequest(new { message = r.ErrorMessage });
    }
}
