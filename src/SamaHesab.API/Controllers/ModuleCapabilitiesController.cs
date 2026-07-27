using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SamaHesab.Application.Settings;
using SamaHesab.Modules.Abstractions;

namespace SamaHesab.API.Controllers;

/// <summary>
/// U-WEB-MODULE-NAV — کلیدهایِ ماژول‌هایِ بارگذاری‌شده و **فعالِ** این شرکت، برایِ هر کاربرِ
/// احرازهویت‌شده. navbar وب از این endpoint استفاده می‌کند تا فقط لینکِ ماژول‌هایِ فعال نشان بدهد.
/// ماژولِ غیرفعال‌شده (در تنظیماتِ شرکتی، U-WEB-MODULE-TOGGLE) از این فهرست حذف می‌شود ⇒ از منو محو می‌شود.
/// </summary>
[ApiController]
[Authorize]
[Route("api/module-capabilities")]
public class ModuleCapabilitiesController : ControllerBase
{
    private readonly IEnumerable<IModule> _loadedModules;
    private readonly IMediator _mediator;
    public ModuleCapabilitiesController(IEnumerable<IModule> loadedModules, IMediator mediator)
    { _loadedModules = loadedModules; _mediator = mediator; }

    [HttpGet]
    public async Task<IActionResult> Keys(CancellationToken ct)
    {
        var settings = await _mediator.Send(new GetCompanySettingsQuery(), ct);
        settings.TryGetValue(CompanySettingKeys.DisabledModules, out var csv);
        var disabled = DisabledModulesHelper.Parse(csv);
        return Ok(_loadedModules.Select(m => m.Key).Where(k => !disabled.Contains(k)).ToArray());
    }
}
