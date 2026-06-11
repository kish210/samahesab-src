using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SamaHesab.Application.Common.Barcode;

namespace SamaHesab.API.Controllers;

/// <summary>کار #۲۷ — سرویس بارکد یکپارچه (مشترک: POS/رسید/حواله/انبارگردانی).</summary>
[ApiController]
[Authorize]
[Route("api/[controller]")]
public class BarcodeController : ControllerBase
{
    private readonly IMediator _mediator;
    public BarcodeController(IMediator mediator) => _mediator = mediator;

    /// <summary>حل یک بارکد به کالا (با پشتیبانی از بارکد وزنی/قیمت‌دار). در صورت نیافتن: 404.</summary>
    [HttpGet("resolve")]
    public async Task<IActionResult> Resolve([FromQuery] string code, CancellationToken ct)
    {
        var hit = await _mediator.Send(new ResolveBarcodeQuery(code), ct);
        return hit is null ? NotFound() : Ok(hit);
    }
}
