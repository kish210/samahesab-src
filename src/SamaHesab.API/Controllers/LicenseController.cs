using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SamaHesab.Application.Licensing;

namespace SamaHesab.API.Controllers;

/// <summary>U-LIC-FREEYEAR — وضعیتِ بنرِ «یک‌سالِ رایگان» برایِ نمایش در وب. اطلاع‌رسانی‌ست، نه دروازه.</summary>
[ApiController]
[Authorize]
[Route("api/license")]
public class LicenseController : ControllerBase
{
    private readonly IServerLicenseStatusProvider _status;
    public LicenseController(IServerLicenseStatusProvider status) { _status = status; }

    [HttpGet("status")]
    public IActionResult Status() => Ok(_status.GetStatus());
}
