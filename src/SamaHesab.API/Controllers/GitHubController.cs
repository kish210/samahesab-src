using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SamaHesab.API.Services;

namespace SamaHesab.API.Controllers;

/// <summary>
/// اتصال به GitHub — ۱) یادداشتِ نسخه‌ها (ریلیزهای مخزنِ عمومیِ kish210/SamaHesab) که تبِ
/// «یادداشتِ نسخه»ی وب را بدونِ نیاز به کانفیگِ Support پر می‌کند؛ ۲) ثبتِ Issue برای گزارشِ باگ.
/// توکنِ GitHub فقط روی سرور نگه داشته می‌شود (GitHubService) — کلاینت هیچ کلیدی نمی‌بیند.
/// </summary>
[ApiController]
[Authorize]
[Route("api/github")]
public class GitHubController : ControllerBase
{
    private readonly GitHubService _github;
    public GitHubController(GitHubService github) => _github = github;

    [HttpGet("releases")]
    public async Task<IActionResult> Releases(CancellationToken ct)
        => Ok(await _github.GetReleasesAsync(ct));

    public record CreateIssueRequest(string Title, string Description);

    [HttpPost("issues")]
    public async Task<IActionResult> CreateIssue([FromBody] CreateIssueRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Title) || string.IsNullOrWhiteSpace(req.Description))
            return BadRequest(new { message = "عنوان و شرحِ Issue الزامی است." });

        try
        {
            var url = await _github.CreateIssueAsync(req.Title.Trim(), req.Description.Trim(), ct);
            return Ok(new { url, message = "گزارش به‌عنوان Issue در GitHub ثبت شد." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(502, new { message = ex.Message });
        }
    }
}
