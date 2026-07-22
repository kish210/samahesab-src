using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SamaHesab.Application.Support.Commands;
using SamaHesab.Application.Support.Queries;
using SamaHesab.Domain.Enums;

namespace SamaHesab.API.Controllers;

/// <summary>
/// U-WEB-SUPPORT — پورتِ وبِ ماژولِ پشتیبانی: تیکت، گزارشِ باگ، مرکزِ راهنما، یادداشتِ نسخه.
/// همهٔ Application/Domain/DB از قبل کامل بود (پیش‌تر فقط دسکتاپ داشت) — این کنترلر صرفاً
/// MediatRِ موجود را expose می‌کند. پشتیبانیِ ریموت/تشخیصی عمداً نیامده (به‌درخواستِ کاربر —
/// نیازمندِ نشستِ زندهٔ دسکتاپ‌اند، روی وب معنا ندارند).
/// </summary>
[ApiController]
[Authorize]
[Route("api/support")]
public class SupportController : ControllerBase
{
    private readonly IMediator _mediator;
    public SupportController(IMediator mediator) => _mediator = mediator;

    // ── تیکت‌ها ──
    [HttpGet("tickets")]
    public async Task<IActionResult> Tickets(CancellationToken ct)
        => Ok(await _mediator.Send(new GetSupportTicketsQuery(), ct));

    public record CreateTicketRequest(string Subject, string Body, SupportCategory Category);

    [HttpPost("tickets")]
    public async Task<IActionResult> CreateTicket([FromBody] CreateTicketRequest req, CancellationToken ct)
    {
        var r = await _mediator.Send(new CreateSupportTicketCommand(req.Subject, req.Body, req.Category), ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { message = r.ErrorMessage });
    }

    public record AddMessageRequest(string Text);

    [HttpPost("tickets/{id:int}/messages")]
    public async Task<IActionResult> AddMessage(int id, [FromBody] AddMessageRequest req, CancellationToken ct)
    {
        var r = await _mediator.Send(new AddTicketMessageCommand(id, req.Text), ct);
        return r.Succeeded ? Ok() : BadRequest(new { message = r.ErrorMessage });
    }

    // ── گزارشِ باگ ──
    public record CreateBugReportRequest(
        string Title, string Description, BugSeverity Severity, SupportCategory Category,
        string? ExpectedResult, string? ActualResult, string? StepsToReproduce, string? ScreenName);

    [HttpPost("bug-reports")]
    public async Task<IActionResult> CreateBugReport([FromBody] CreateBugReportRequest req, CancellationToken ct)
    {
        var cmd = new CreateBugReportCommand(req.Title, req.Description, req.Severity, req.Category,
            req.ExpectedResult, req.ActualResult, req.StepsToReproduce, null, req.ScreenName, null);
        var r = await _mediator.Send(cmd, ct);
        return r.Succeeded ? Ok(r.Value) : BadRequest(new { message = r.ErrorMessage });
    }

    // ── مرکزِ راهنما (دانشنامه) ──
    [HttpGet("knowledge-base")]
    public async Task<IActionResult> KnowledgeBase([FromQuery] string? search, CancellationToken ct)
        => Ok(await _mediator.Send(new SyncKnowledgeArticlesCommand(search), ct));

    // ── یادداشتِ نسخه ──
    [HttpGet("release-notes")]
    public async Task<IActionResult> ReleaseNotes(CancellationToken ct)
        => Ok(await _mediator.Send(new SyncReleaseNotesCommand(), ct));
}
