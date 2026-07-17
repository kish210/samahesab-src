using System.IO.Compression;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SamaHesab.Infrastructure.Modules;
using SamaHesab.Modules.Abstractions;

namespace SamaHesab.API.Controllers;

/// <summary>
/// U-MODULE-INSTALL — مدیریتِ ماژول‌هایِ سمتِ سرور برایِ کلاینتِ وب/دسکتاپ:
/// فهرستِ ماژول‌هایِ بارگذاری‌شده + نصبِ ماژولِ نو از فایلِ .mspkg (آپلود، بدونِ گیت/rebuild).
/// چون بارگذاریِ IModule در startup است، ماژولِ تازه‌نصب‌شده با ری‌استارتِ سرور فعال می‌شود
/// (پاسخِ install فیلدِ restartRequired برمی‌گرداند).
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "ADMIN")]
public class ModulesController : ControllerBase
{
    private readonly IEnumerable<IModule> _loadedModules;
    private readonly IConfiguration _config;
    private readonly ILogger<ModulesController> _logger;

    public ModulesController(IEnumerable<IModule> loadedModules, IConfiguration config, ILogger<ModulesController> logger)
    {
        _loadedModules = loadedModules;
        _config = config;
        _logger = logger;
    }

    public record ModuleRow(string Key, string DisplayName, string Version, string Source);

    private string ModulesDir => ModuleLoader.ServerModulesDirectory(_config["Modules:Directory"]);

    /// <summary>فهرستِ ماژول‌هایِ بارگذاری‌شده (bundle) + فایل‌هایِ نصب‌شدهٔ منتظرِ ری‌استارت.</summary>
    [HttpGet]
    public IActionResult List()
    {
        var loaded = _loadedModules
            .Select(m => new ModuleRow(m.Key, m.DisplayName, m.Version, "بارگذاری‌شده"))
            .ToList();
        var loadedKeys = loaded.Select(r => r.Key).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // فایل‌هایِ .mspkgِ روی فولدرِ سرور که هنوز کلیدشان در بارگذاری‌شده‌ها نیست ⇒ منتظرِ ری‌استارت.
        var pending = new List<ModuleRow>();
        try
        {
            if (Directory.Exists(ModulesDir))
                foreach (var pkg in Directory.GetFiles(ModulesDir, "*.mspkg"))
                {
                    var key = Path.GetFileNameWithoutExtension(pkg);
                    if (!loadedKeys.Contains(key))
                        pending.Add(new ModuleRow(key, key, "?", "نصب‌شده — نیازمندِ ری‌استارت"));
                }
        }
        catch (Exception ex) { _logger.LogWarning(ex, "خواندنِ فولدرِ ماژول‌ها ناموفق بود"); }

        return Ok(loaded.Concat(pending).OrderBy(r => r.DisplayName));
    }

    /// <summary>
    /// نصبِ ماژول از فایلِ .mspkg (آپلودِ multipart). فایل اعتبارسنجی می‌شود (zipِ معتبر حاویِ
    /// SamaHesab.Modules.*.dll) و در فولدرِ سرور ذخیره می‌شود. فعال‌سازی نیازمندِ ری‌استارتِ سرور است.
    /// </summary>
    [HttpPost("install")]
    [RequestSizeLimit(50 * 1024 * 1024)]   // ماژول‌ها کوچک‌اند؛ سقفِ ۵۰MB برایِ جلوگیری از سوءاستفاده
    public async Task<IActionResult> Install(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "فایلی انتخاب نشده است." });
        if (!file.FileName.EndsWith(".mspkg", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "فقط فایلِ .mspkg پذیرفته می‌شود." });

        // در بافر بخوان تا هم اعتبارسنجی (zip + محتوای ماژول) و هم ذخیره از همان بایت‌ها انجام شود.
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms, ct);
        ms.Position = 0;

        string moduleKey;
        try
        {
            using var zip = new ZipArchive(ms, ZipArchiveMode.Read, leaveOpen: true);
            var dll = zip.Entries.FirstOrDefault(e =>
                e.Name.StartsWith("SamaHesab.Modules.", StringComparison.OrdinalIgnoreCase) &&
                e.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase));
            if (dll is null)
                return BadRequest(new { message = "این فایل یک بستهٔ ماژولِ معتبر نیست (SamaHesab.Modules.*.dll یافت نشد)." });
            moduleKey = Path.GetFileNameWithoutExtension(file.FileName);
        }
        catch (InvalidDataException)
        {
            return BadRequest(new { message = "فایل یک آرشیوِ zipِ معتبر (.mspkg) نیست." });
        }

        try
        {
            Directory.CreateDirectory(ModulesDir);
            // نامِ فایل را sanitize کن (فقط نامِ فایل، بدونِ مسیر — جلوگیری از path traversal).
            var safeName = Path.GetFileName(file.FileName);
            var dest = Path.Combine(ModulesDir, safeName);
            ms.Position = 0;
            using (var fs = System.IO.File.Create(dest)) await ms.CopyToAsync(fs, ct);
            _logger.LogInformation("ماژولِ {Key} در {Dest} نصب شد", moduleKey, dest);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ذخیرهٔ ماژول ناموفق بود");
            return StatusCode(500, new { message = "ذخیرهٔ فایلِ ماژول روی سرور ناموفق بود: " + ex.GetBaseException().Message });
        }

        var alreadyLoaded = _loadedModules.Any(m => string.Equals(m.Key, moduleKey, StringComparison.OrdinalIgnoreCase));
        return Ok(new
        {
            installed = true,
            key = moduleKey,
            restartRequired = true,
            message = alreadyLoaded
                ? "به‌روزرسانیِ ماژول ذخیره شد. برایِ اعمال، سرور را یک‌بار ری‌استارت کنید."
                : "ماژول نصب شد. برایِ فعال‌سازی، سرور را یک‌بار ری‌استارت کنید."
        });
    }

    /// <summary>حذفِ بستهٔ نصب‌شدهٔ یک ماژول (فقط فایلِ فولدرِ سرور؛ ماژولِ bundle حذف‌شدنی نیست).
    /// اثرگذاری پس از ری‌استارت.</summary>
    [HttpDelete("{key}")]
    public IActionResult Remove(string key)
    {
        var safeKey = Path.GetFileName(key);   // جلوگیری از path traversal
        var pkg = Path.Combine(ModulesDir, safeKey + ".mspkg");
        var extractDir = Path.Combine(ModulesDir, safeKey);
        var removed = false;
        try
        {
            if (System.IO.File.Exists(pkg)) { System.IO.File.Delete(pkg); removed = true; }
            if (Directory.Exists(extractDir)) { Directory.Delete(extractDir, recursive: true); removed = true; }
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "حذفِ ماژول ناموفق بود: " + ex.GetBaseException().Message });
        }
        if (!removed)
            return NotFound(new { message = "بستهٔ نصب‌شده‌ای با این کلید یافت نشد (ماژولِ داخلیِ برنامه حذف‌شدنی نیست)." });
        return Ok(new { removed = true, restartRequired = true, message = "ماژول حذف شد. برایِ اعمال، سرور را ری‌استارت کنید." });
    }
}
