using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SamaHesab.Application.CRM.Commands;
using SamaHesab.Application.CRM.Queries;

namespace SamaHesab.API.Controllers;

/// <summary>کار #۳۸ — باشگاه مشتریان: موجودی امتیاز، کسب (از خرید) و استفاده.</summary>
[ApiController]
[Authorize]
[Route("api/[controller]")]
public class LoyaltyController : ControllerBase
{
    private readonly IMediator _mediator;
    public LoyaltyController(IMediator mediator) => _mediator = mediator;

    public record AwardRequest(int CustomerId, decimal PurchaseAmount, string Reason);
    public record RedeemRequest(int CustomerId, int Points, string Reason);

    [HttpGet("{customerId:int}")]
    public async Task<IActionResult> Get(int customerId, CancellationToken ct)
        => Ok(await _mediator.Send(new GetCustomerLoyaltyQuery(customerId), ct));

    [HttpPost("award")]
    public async Task<IActionResult> Award([FromBody] AwardRequest req, CancellationToken ct)
    {
        var r = await _mediator.Send(new AwardLoyaltyPointsCommand(req.CustomerId, req.PurchaseAmount, req.Reason), ct);
        return r.Succeeded ? Ok(new { earnedPoints = r.Value }) : BadRequest(new { message = r.ErrorMessage });
    }

    [HttpPost("redeem")]
    public async Task<IActionResult> Redeem([FromBody] RedeemRequest req, CancellationToken ct)
    {
        var r = await _mediator.Send(new RedeemLoyaltyPointsCommand(req.CustomerId, req.Points, req.Reason), ct);
        return r.Succeeded ? Ok() : BadRequest(new { message = r.ErrorMessage });
    }
}
