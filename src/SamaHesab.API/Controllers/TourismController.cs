using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SamaHesab.Application.Accounting.Dimensions;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Modules.Tourism.Application;
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

    /// <summary>تنظیماتِ نگاشتِ حساب‌هایِ گردشگری — پیش‌نیازِ ثبتِ فروش (بدونِ این، «ثبتِ فروش» رد می‌شود).</summary>
    [HttpGet("settings")]
    public async Task<IActionResult> GetSettings(CancellationToken ct)
        => Ok(await _mediator.Send(new GetTourismSettingsQuery(), ct));

    [HttpPost("settings")]
    public async Task<IActionResult> SaveSettings([FromBody] TourismSettingsDto dto, CancellationToken ct)
    {
        var r = await _mediator.Send(new SaveTourismSettingsCommand(dto), ct);
        return r.Succeeded ? Ok(new { id = r.Value }) : BadRequest(new { message = r.ErrorMessage });
    }

    /// <summary>فهرستِ محصولات/خدماتِ گردشگری (مدیریت — شاملِ تأمین‌کننده/بها/پورسانت).</summary>
    [HttpGet("products")]
    public async Task<IActionResult> GetProducts([FromQuery] bool activeOnly = true, CancellationToken ct = default)
        => Ok(await _mediator.Send(new GetTourismProductsQuery(activeOnly), ct));

    [HttpPost("products")]
    public async Task<IActionResult> SaveProduct([FromBody] SaveTourismProductCommand cmd, CancellationToken ct)
    {
        var r = await _mediator.Send(cmd, ct);
        return r.Succeeded ? Ok(new { id = r.Value }) : BadRequest(new { message = r.ErrorMessage });
    }

    [HttpDelete("products/{id:int}")]
    public async Task<IActionResult> DeleteProduct(int id, CancellationToken ct)
    {
        var r = await _mediator.Send(new DeleteTourismProductCommand(id), ct);
        return r.Succeeded ? Ok() : BadRequest(new { message = r.ErrorMessage });
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
