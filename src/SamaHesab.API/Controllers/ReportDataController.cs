using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SamaHesab.Application.Inventory.Queries;
using SamaHesab.Application.Reports.Queries;

namespace SamaHesab.API.Controllers;

/// <summary>
/// دادهٔ JSONِ گزارش‌های هسته (برای وب/موبایل) — جدا از خروجی فایل (`/api/reports/export`).
/// </summary>
[ApiController]
[Route("api/reports")]
[Authorize]
public class ReportDataController : ControllerBase
{
    private readonly IMediator _mediator;
    public ReportDataController(IMediator mediator) => _mediator = mediator;

    [HttpGet("trial-balance")]
    public async Task<IActionResult> TrialBalance([FromQuery] string from, [FromQuery] string to,
        [FromQuery] int? costCenterId, [FromQuery] int? projectId, [FromQuery] int? branchId)
        => Ok(await _mediator.Send(new GetTrialBalanceQuery(from, to, costCenterId, projectId, branchId)));

    [HttpGet("general-ledger")]
    public async Task<IActionResult> GeneralLedger([FromQuery] string from, [FromQuery] string to,
        [FromQuery] int? accountId, [FromQuery] int? costCenterId, [FromQuery] int? projectId, [FromQuery] int? branchId)
        => Ok(await _mediator.Send(new GetGeneralLedgerQuery(from, to, accountId, costCenterId, projectId, branchId)));

    [HttpGet("profit-loss")]
    public async Task<IActionResult> ProfitLoss([FromQuery] string from, [FromQuery] string to, [FromQuery] int? branchId)
        => Ok(await _mediator.Send(new GetProfitLossQuery(from, to, branchId)));

    [HttpGet("balance-sheet")]
    public async Task<IActionResult> BalanceSheet([FromQuery] string from, [FromQuery] string to, [FromQuery] int? branchId)
        => Ok(await _mediator.Send(new GetBalanceSheetQuery(from, to, branchId)));

    [HttpGet("cash-flow")]
    public async Task<IActionResult> CashFlow([FromQuery] string from, [FromQuery] string to, [FromQuery] int? branchId)
        => Ok(await _mediator.Send(new GetCashFlowQuery(from, to, branchId)));

    [HttpGet("inventory-valuation")]
    public async Task<IActionResult> InventoryValuation([FromQuery] int? warehouseId, [FromQuery] string? search)
        => Ok(await _mediator.Send(new GetInventoryValuationQuery(warehouseId, search)));
}
