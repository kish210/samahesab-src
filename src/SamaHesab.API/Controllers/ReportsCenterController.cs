using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SamaHesab.Application.Reports.Queries;

namespace SamaHesab.API.Controllers;

/// <summary>
/// U-WEB-REPORTS-CENTER — «مرکزِ گزارشات» وب. Application (RunReportQuery، ۱۸ گزارشِ هسته)
/// از قبل کامل بود (پورت‌شده برایِ دسکتاپ در کارِ #۸۴-۸۸)، ولی هیچ اندپوینتِ HTTP‌ای برایِ
/// آن وجود نداشت. SalaryReport/AttendanceSummary عمداً این‌جا نیستند — ماژول‌هایِ اختیاریِ
/// HR/حضوروغیاب که هنوز آمادگیِ وبشان بررسی نشده (طبقِ CLAUDE.md، هسته به ماژول وابسته نیست).
/// </summary>
[ApiController]
[Authorize]
[Route("api/reports")]
public class ReportsCenterController : ControllerBase
{
    private readonly IMediator _mediator;
    public ReportsCenterController(IMediator mediator) => _mediator = mediator;

    [HttpGet("run")]
    public async Task<IActionResult> Run(string code, string from, string to, CancellationToken ct)
    {
        var result = await _mediator.Send(new RunReportQuery(code, from, to), ct);
        return Ok(result);
    }
}
