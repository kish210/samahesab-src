using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SamaHesab.Application.Documents;
using SamaHesab.Application.Sales.Queries;
using SamaHesab.Application.Purchase.Queries;
using SamaHesab.Application.Settings;

namespace SamaHesab.API.Controllers;

/// <summary>قالب‌هایِ چاپِ اسناد (سربرگ/بدنه/فوتر با توکن) — پیش‌تر فقط دسکتاپ داشت
/// (`DocumentTemplatesViewModel`)؛ وب هیچ راهی برایِ مدیریت/نصبِ قالب‌هایِ پیش‌فرض نداشت.</summary>
[ApiController]
[Authorize]
[Route("api/document-templates")]
public class DocumentTemplatesController : ControllerBase
{
    private readonly IMediator _mediator;
    public DocumentTemplatesController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] string documentType, CancellationToken ct)
        => Ok(await _mediator.Send(new GetDocumentTemplatesQuery(documentType), ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get(int id, CancellationToken ct)
    {
        var t = await _mediator.Send(new GetDocumentTemplateQuery(id), ct);
        return t is null ? NotFound(new { message = "قالب یافت نشد." }) : Ok(t);
    }

    [HttpPost]
    public async Task<IActionResult> Save([FromBody] SaveDocumentTemplateCommand cmd, CancellationToken ct)
    {
        var r = await _mediator.Send(cmd, ct);
        return r.Succeeded ? Ok(new { id = r.Value }) : BadRequest(new { message = r.ErrorMessage });
    }

    [HttpPost("{id:int}/set-default")]
    public async Task<IActionResult> SetDefault(int id, CancellationToken ct)
    {
        var r = await _mediator.Send(new SetDefaultTemplateCommand(id), ct);
        return r.Succeeded ? Ok() : BadRequest(new { message = r.ErrorMessage });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var r = await _mediator.Send(new DeleteDocumentTemplateCommand(id), ct);
        return r.Succeeded ? Ok() : BadRequest(new { message = r.ErrorMessage });
    }

    /// <summary>نصبِ idempotentِ پکِ قالب‌هایِ پیش‌فرض از پوشهٔ `Templates` کنارِ خودِ API
    /// (کپی‌شده در publish، هم‌الگو با `SamaHesab.WPF.csproj`).</summary>
    [HttpPost("install-builtin")]
    public async Task<IActionResult> InstallBuiltIn(CancellationToken ct)
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "Templates");
        var res = await _mediator.Send(new InstallBuiltInTemplatesCommand(dir), ct);
        return Ok(res);
    }

    public record PreviewRequest(string? HeaderHtml, string BodyHtml, string? FooterHtml);

    /// <summary>پیش‌نمایشِ HTMLِ یک قالب با دادهٔ نمونه — بدونِ نیازِ سندِ واقعی.</summary>
    [HttpPost("preview")]
    public IActionResult Preview([FromBody] PreviewRequest req)
    {
        var fields = new Dictionary<string, string?>
        {
            ["InvoiceNumber"] = "F-1001", ["InvoiceDate"] = "1405/03/25", ["CustomerName"] = "مشتریِ نمونه",
            ["CustomerCode"] = "100", ["TotalAmount"] = "12,500,000", ["Tax"] = "1,125,000",
            ["Discount"] = "0", ["BranchName"] = "سما حساب",
        };
        var rows = new List<IReadOnlyDictionary<string, string?>>
        {
            new Dictionary<string, string?> { ["ProductName"] = "کالا الف", ["Quantity"] = "2", ["UnitPrice"] = "1,000,000", ["LineTotal"] = "2,000,000" },
            new Dictionary<string, string?> { ["ProductName"] = "کالا ب", ["Quantity"] = "3", ["UnitPrice"] = "3,500,000", ["LineTotal"] = "10,500,000" },
        };
        var data = DocumentData.Of(fields, rows);
        var html = DocumentTemplateEngine.Render(req.HeaderHtml, data)
                 + DocumentTemplateEngine.Render(req.BodyHtml, data)
                 + DocumentTemplateEngine.Render(req.FooterHtml, data);
        return Ok(new { html });
    }

    /// <summary>
    /// U-WEB-TEMPLATES-BIND — رندرِ واقعیِ چاپِ یک فاکتورِ فروش/خرید با قالبِ پیش‌فرضِ همان نوعِ
    /// سند (اگر شرکت قالبِ سفارشی تعیین کرده باشد). اگر هیچ قالبِ پیش‌فرضی نباشد `html: null`
    /// برمی‌گردد — کلاینت در این حالت به layoutِ هاردکدِ فعلیِ صفحهٔ فاکتور برمی‌گردد (بدونِ شکستنِ
    /// چاپِ ازقبل‌تست‌شده برایِ شرکت‌هایی که هنوز قالبِ سفارشی نساخته‌اند).
    /// </summary>
    [HttpGet("render-invoice")]
    public async Task<IActionResult> RenderInvoice([FromQuery] string documentType, [FromQuery] int entityId, CancellationToken ct)
    {
        if (documentType != "SalesInvoice" && documentType != "PurchaseInvoice")
            return BadRequest(new { message = "نوعِ سند برایِ رندر پشتیبانی نمی‌شود." });

        var list = await _mediator.Send(new GetDocumentTemplatesQuery(documentType), ct);
        // فقط قالبِ پیش‌فرضِ *سفارشیِ شرکت* (نه قالب‌هایِ سیستمیِ پکِ نمونه) رندرِ واقعیِ فاکتور را
        // به‌جایِ layoutِ هاردکدِ ازقبل‌تست‌شده می‌گیرد — تا نصبِ پکِ نمونه (که یکی از آن‌ها ممکن است
        // پیش‌فرض علامت‌گذاری شود) به‌طورِ ناخواسته چاپِ همهٔ شرکت‌ها را عوض نکند.
        var defaultTemplate = list.FirstOrDefault(t => t.IsDefault && !t.IsSystem);
        if (defaultTemplate is null) return Ok(new { html = (string?)null });

        var full = await _mediator.Send(new GetDocumentTemplateQuery(defaultTemplate.Id), ct);
        if (full is null) return Ok(new { html = (string?)null });

        var companySettings = await _mediator.Send(new GetCompanySettingsQuery(), ct);
        companySettings.TryGetValue(CompanySettingKeys.CompanyName, out var companyName);

        DocumentData data;
        if (documentType == "SalesInvoice")
        {
            var inv = await _mediator.Send(new GetSalesInvoiceByIdQuery(entityId), ct);
            if (inv is null) return NotFound(new { message = "فاکتور یافت نشد." });
            data = InvoiceDocumentData.FromSalesInvoice(inv, companyName);
        }
        else
        {
            var inv = await _mediator.Send(new GetPurchaseInvoiceByIdQuery(entityId), ct);
            if (inv is null) return NotFound(new { message = "فاکتور یافت نشد." });
            data = InvoiceDocumentData.FromPurchaseInvoice(inv, companyName);
        }

        var html = DocumentTemplateEngine.Render(full.HeaderHtml, data)
                 + DocumentTemplateEngine.Render(full.BodyHtml, data)
                 + DocumentTemplateEngine.Render(full.FooterHtml, data);
        return Ok(new { html });
    }
}
