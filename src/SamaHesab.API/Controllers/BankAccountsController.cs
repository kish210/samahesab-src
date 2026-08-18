using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SamaHesab.Application.Accounting.Commands;
using SamaHesab.Application.Accounting.Queries;

namespace SamaHesab.API.Controllers;

/// <summary>
/// حساب‌های بانکی + مغایرت‌گیری بانکی (U-BANK-RECON-WEB) — پورتِ وبِ
/// BankReconciliationView دسکتاپ روی همان موتورهای خالص Application.
/// </summary>
[ApiController]
[Authorize]
[Route("api/[controller]")]
public class BankAccountsController : ControllerBase
{
    private readonly IMediator _mediator;
    public BankAccountsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] bool activeOnly, CancellationToken ct)
        => Ok(await _mediator.Send(new GetBankAccountsQuery(activeOnly), ct));

    /// <summary>دفترِ بانکِ یک حساب در بازهٔ تاریخ (ردیف‌های سند روی حسابِ کلِ بانک).</summary>
    [HttpGet("{id:int}/ledger")]
    public async Task<IActionResult> Ledger(int id, [FromQuery] string from, [FromQuery] string to, CancellationToken ct)
        => Ok(await _mediator.Send(new GetBankLedgerQuery(id, from, to), ct));

    /// <summary>اجرای تطبیق خودکار: دفترِ باز + صورت‌حسابِ CSV → منطبق/نامنطبقِ هر طرف.</summary>
    [HttpPost("{id:int}/reconcile")]
    public async Task<IActionResult> Reconcile(int id, [FromBody] RunReconcileRequest body, CancellationToken ct)
        => Ok(await _mediator.Send(new RunBankReconciliationQuery(id, body.From, body.To, body.StatementCsv), ct));

    /// <summary>ثبتِ ماندگارِ ردیف‌های تطبیق‌شده تا در بارگذاری‌های بعدی تکرار نشوند.</summary>
    [HttpPost("{id:int}/reconcile/commit")]
    public async Task<IActionResult> Commit(int id, [FromBody] CommitReconcileRequest body, CancellationToken ct)
    {
        var r = await _mediator.Send(new CommitBankReconciliationCommand(id, body.VoucherItemIds, body.Date), ct);
        return r.Succeeded ? Ok(new { added = r.Value }) : BadRequest(new { message = r.ErrorMessage });
    }
}

public record RunReconcileRequest(string From, string To, string StatementCsv);
public record CommitReconcileRequest(List<int> VoucherItemIds, string Date);
