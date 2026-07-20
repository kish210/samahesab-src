using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SamaHesab.Modules.Abstractions;

namespace SamaHesab.API.Controllers;

/// <summary>
/// U-WEB-MODULE-NAV — کلیدهایِ ماژول‌هایِ بارگذاری‌شده روی سرور، برایِ **هر کاربرِ احرازهویت‌شده**
/// (نه فقط ADMIN مثلِ `ModulesController` که نصب/حذف را مدیریت می‌کند). navbar وب از این
/// endpoint استفاده می‌کند تا فقط لینکِ ماژول‌هایی که واقعاً روی این سرور فعالند نشان بدهد —
/// نه هر ماژولِ اختیاری را کورکورانه (که اگر بارگذاری نشده باشد، صفحه‌اش خطا می‌داد).
/// </summary>
[ApiController]
[Authorize]
[Route("api/module-capabilities")]
public class ModuleCapabilitiesController : ControllerBase
{
    private readonly IEnumerable<IModule> _loadedModules;
    public ModuleCapabilitiesController(IEnumerable<IModule> loadedModules) { _loadedModules = loadedModules; }

    [HttpGet]
    public IActionResult Keys() => Ok(_loadedModules.Select(m => m.Key).ToArray());
}
