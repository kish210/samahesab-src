using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SamaHesab.Application.Sales.Commands;

namespace SamaHesab.API.Controllers;

[ApiController]
[Authorize(Roles = "ADMIN")]
[Route("api/sales")]
public class SalesController : ControllerBase
{
    private readonly IMediator _mediator;
    public SalesController(IMediator mediator) => _mediator = mediator;

    /// <summary>Create a sales invoice (reduces stock + posts the automatic voucher).</summary>
    [HttpPost("invoices")]
    public async Task<IActionResult> Create([FromBody] CreateSalesInvoiceCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return result.Succeeded
            ? Ok(new { invoiceId = result.Value })
            : BadRequest(new { message = result.ErrorMessage });
    }
}
