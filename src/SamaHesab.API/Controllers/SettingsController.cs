using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SamaHesab.Application.Settings;

namespace SamaHesab.API.Controllers;

/// <summary>تنظیماتِ شرکتی (نام/کدِ ملی/کدِ اقتصادی/تلفن/آدرس) — برایِ سربرگِ چاپیِ فاکتور/رسید در وب.</summary>
[ApiController]
[Authorize]
[Route("api/settings")]
public class SettingsController : ControllerBase
{
    private readonly IMediator _mediator;
    public SettingsController(IMediator mediator) => _mediator = mediator;

    [HttpGet("company")]
    public async Task<IActionResult> Company(CancellationToken ct)
        => Ok(await _mediator.Send(new GetCompanySettingsQuery(), ct));

    /// <summary>
    /// ذخیرهٔ تنظیماتِ شرکت (کلید→مقدار). فقط کلیدهایِ شناخته‌شدهٔ `CompanySettingKeys` پذیرفته
    /// می‌شوند تا این اندپوینت به یک انبارِ کلید-مقدارِ دلخواه تبدیل نشود.
    /// </summary>
    [HttpPut("company")]
    public async Task<IActionResult> SaveCompany([FromBody] Dictionary<string, string?> values, CancellationToken ct)
    {
        if (values is null || values.Count == 0)
            return BadRequest(new { message = "داده‌ای برایِ ذخیره ارسال نشده است." });

        var allowed = new HashSet<string>
        {
            CompanySettingKeys.CompanyName, CompanySettingKeys.CompanyNationalId,
            CompanySettingKeys.CompanyEconomicCode, CompanySettingKeys.CompanyPhone,
            CompanySettingKeys.CompanyAddress,
        };

        var unknown = values.Keys.Where(k => !allowed.Contains(k)).ToList();
        if (unknown.Count > 0)
            return BadRequest(new { message = $"کلیدِ ناشناخته: {string.Join("، ", unknown)}" });

        foreach (var (key, value) in values)
        {
            var r = await _mediator.Send(new SaveCompanySettingCommand(key, value), ct);
            if (!r.Succeeded) return BadRequest(new { message = r.ErrorMessage });
        }
        return Ok(await _mediator.Send(new GetCompanySettingsQuery(), ct));
    }
}
