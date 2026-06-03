using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SamaHesab.Application.Purchase.Commands;

namespace SamaHesab.API.Controllers;

[ApiController]
[Authorize(Roles = "ADMIN")]
[Route("api/purchase")]
public class PurchaseController : ControllerBase
{
    private readonly IMediator _mediator;
    public PurchaseController(IMediator mediator) => _mediator = mediator;

    /// <summary>Create a purchase invoice (increases stock + posts the automatic voucher).</summary>
    [HttpPost("invoices")]
    public async Task<IActionResult> Create([FromBody] CreatePurchaseInvoiceCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return result.Succeeded
            ? Ok(new { invoiceId = result.Value })
            : BadRequest(new { message = result.ErrorMessage });
    }
}
