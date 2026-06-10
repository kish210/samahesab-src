using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SamaHesab.Application.Automation.Queries;
using SamaHesab.Application.BI.Queries;

namespace SamaHesab.API.Controllers;

/// <summary>
/// هوش تجاری و داشبوردهای نقش‌محور — فاز محصول‌سازی (P1).
/// همه‌ی عملیات از طریق همین API برای کلاینت‌های دسکتاپ/وب/موبایل قابل‌مصرف است.
/// </summary>
[ApiController]
[Authorize(Roles = "ADMIN")]
[Route("api/[controller]")]
public class AnalyticsController : ControllerBase
{
    private readonly IMediator _mediator;
    public AnalyticsController(IMediator mediator) => _mediator = mediator;

    /// <summary>پرفروش‌ترین مشتریان در بازه.</summary>
    [HttpGet("top-customers")]
    public async Task<IActionResult> TopCustomers(
        [FromQuery] string from, [FromQuery] string to, [FromQuery] int take, CancellationToken ct)
        => Ok(await _mediator.Send(new GetTopCustomersQuery(from, to, take <= 0 ? 10 : take), ct));

    /// <summary>روند فروش ماهانه در بازه.</summary>
    [HttpGet("sales-trend")]
    public async Task<IActionResult> SalesTrend(
        [FromQuery] string from, [FromQuery] string to, CancellationToken ct)
        => Ok(await _mediator.Send(new GetSalesTrendQuery(from, to), ct));

    /// <summary>تحلیل سود + پرفروش‌ترین کالاها در بازه.</summary>
    [HttpGet("profit")]
    public async Task<IActionResult> Profit(
        [FromQuery] string from, [FromQuery] string to, [FromQuery] int topProducts, CancellationToken ct)
        => Ok(await _mediator.Send(new GetProfitAnalysisQuery(from, to, topProducts <= 0 ? 10 : topProducts), ct));

    /// <summary>داشبورد مدیر/مالک (KPIهای کلیدی).</summary>
    [HttpGet("dashboard/manager")]
    public async Task<IActionResult> ManagerDashboard([FromQuery] string today, CancellationToken ct)
        => Ok(await _mediator.Send(new GetManagerDashboardQuery(today), ct));

    /// <summary>اعلان‌های عملیاتی لحظه‌ای (سررسید چک + کسری موجودی).</summary>
    [HttpGet("alerts")]
    public async Task<IActionResult> Alerts([FromQuery] string today, CancellationToken ct)
        => Ok(await _mediator.Send(new GetAlertsQuery(today), ct));
}
