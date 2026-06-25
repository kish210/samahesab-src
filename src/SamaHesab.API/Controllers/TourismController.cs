using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SamaHesab.Application.Accounting.Dimensions;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Modules.Tourism.Application.Commands;
using SamaHesab.Modules.Tourism.Application.Queries;

namespace SamaHesab.API.Controllers;

/// <summary>
/// SP-2 — APIِ پنلِ فروشِ گردشگری (فروشنده‌محور). برخلافِ بقیهٔ کنترلرها فقط [Authorize]
/// است (نه ADMIN) تا فروشنده‌ها هم بتوانند بفروشند؛ هویتِ فروشنده خودکار از JWT می‌آید.
/// </summary>
[ApiController]
[Authorize]
[Route("api/tourism")]
public class TourismController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;

    public TourismController(IMediator mediator, ICurrentUserService currentUser)
    { _mediator = mediator; _currentUser = currentUser; }

    /// <summary>زمینهٔ پنل: شعبه + سالِ مالیِ فعال + هویتِ فروشنده (برای ساختِ فروش بدونِ ورودِ دستی).</summary>
    [HttpGet("context")]
    public async Task<IActionResult> Context(CancellationToken ct)
    {
        var years = await _mediator.Send(new GetFiscalYearsQuery(), ct);
        var active = years.FirstOrDefault(f => f.IsActive && !f.IsClosed)
                     ?? years.FirstOrDefault(f => !f.IsClosed)
                     ?? years.FirstOrDefault();
        return Ok(new
        {
            branchId = _currentUser.BranchId ?? 1,
            fiscalYearId = active?.Id ?? 0,
            fiscalYearTitle = active?.Title,
            salespersonPartyId = _currentUser.SalespersonPartyId,
            fullName = _currentUser.FullName,
            isSeller = _currentUser.SalespersonPartyId is > 0,
        });
    }

    /// <summary>فهرستِ محصولاتِ گردشگری با قیمت و ماندهٔ ظرفیت (برای انتخاب در پنل).</summary>
    [HttpGet("availability")]
    public async Task<IActionResult> Availability([FromQuery] bool onlyActive = true, CancellationToken ct = default)
        => Ok(await _mediator.Send(new GetTourismAvailabilityQuery(onlyActive), ct));

    /// <summary>فهرستِ فروش‌های گردشگری (برای نمایشِ تاریخچه در پنل).</summary>
    [HttpGet("sales")]
    public async Task<IActionResult> Sales([FromQuery] string? from, [FromQuery] string? to, CancellationToken ct)
        => Ok(await _mediator.Send(new GetTourismSalesQuery(from, to), ct));

    /// <summary>
    /// ثبتِ فروشِ گردشگری. فروشنده خودکار از کاربرِ لاگین‌شده تعیین می‌شود (SellerResolver)؛
    /// پنل می‌تواند SalespersonPartyId را ۰ بفرستد. شعبه/سالِ مالی از endpointِ context می‌آید.
    /// </summary>
    [HttpPost("sales")]
    public async Task<IActionResult> CreateSale([FromBody] CreateTourismSaleCommand command, CancellationToken ct)
    {
        var result = await _mediator.Send(command, ct);
        return result.Succeeded
            ? Ok(new { saleId = result.Value })
            : BadRequest(new { message = result.ErrorMessage });
    }
}
