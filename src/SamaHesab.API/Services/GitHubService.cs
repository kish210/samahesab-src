using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SamaHesab.API.Services;

/// <summary>
/// اتصال به GitHub (REST API) از سمت سرور — توکن هرگز به کلاینت نمی‌رسد.
/// ۱) خواندنِ یادداشتِ نسخه‌های ریلیز از مخزنِ عمومیِ kish210/SamaHesab (بدون توکن هم کار می‌کند؛
///    توکن فقط سقفِ نرخِ ۵۰۰۰/ساعت را می‌دهد). ۲) ثبتِ Issue برای گزارشِ باگ (نیازمندِ توکن).
/// پیکربندی: بخشِ GitHub در appsettings.json یا متغیرِ محیطیِ GITHUB_TOKEN (اولویت با appsettings).
/// </summary>
public class GitHubService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<GitHubService> _logger;
    private readonly string _token;
    private readonly string _owner;
    private readonly string _repo;

    public GitHubService(HttpClient http, IConfiguration config, ILogger<GitHubService> logger)
    {
        _http = http;
        _config = config;
        _logger = logger;
        _http.BaseAddress = new Uri("https://api.github.com");
        // GitHub API بدون User-Agent درخواست را رد می‌کند.
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("SamaHesab-ERP/2.9");
        _http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        _token = _config["GitHub:Token"] ?? Environment.GetEnvironmentVariable("GITHUB_TOKEN") ?? "";
        _owner = string.IsNullOrWhiteSpace(_config["GitHub:Owner"]) ? "kish210" : _config["GitHub:Owner"]!;
        _repo = string.IsNullOrWhiteSpace(_config["GitHub:Repo"]) ? "SamaHesab" : _config["GitHub:Repo"]!;
    }

    public bool HasToken => !string.IsNullOrWhiteSpace(_token);

    public record ReleaseDto(string TagName, string? Name, string? Body, DateTimeOffset? PublishedAt, string? HtmlUrl);

    /// <summary>ریلیزهای مخزن (عمومی) — حداکثر ۳۰ نسخهٔ اخیر. در خطا: فهرستِ خالی + لاگ (fail-soft تا صفحه نشکند).</summary>
    public async Task<IReadOnlyList<ReleaseDto>> GetReleasesAsync(CancellationToken ct = default)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, $"/repos/{_owner}/{_repo}/releases?per_page=30");
            if (HasToken) req.Headers.Authorization = new("Bearer", _token);

            using var res = await _http.SendAsync(req, ct);
            if (!res.IsSuccessStatusCode)
            {
                _logger.LogWarning("GitHub releases: HTTP {Status} برای {Owner}/{Repo}", (int)res.StatusCode, _owner, _repo);
                return Array.Empty<ReleaseDto>();
            }

            await using var stream = await res.Content.ReadAsStreamAsync(ct);
            var items = await JsonSerializer.DeserializeAsync<List<GhRelease>>(stream, JsonOpts, ct);
            return items?.Select(r => new ReleaseDto(r.TagName, r.Name, r.Body, r.PublishedAt, r.HtmlUrl))
                         .ToList() ?? new List<ReleaseDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "خطا در خواندنِ ریلیزهای GitHub");
            return Array.Empty<ReleaseDto>();
        }
    }

    /// <summary>ثبتِ Issue در مخزن — نیازمندِ توکن با دسترسیِ «Issues: write» (یا repo). در نبودِ توکن InvalidOperationException.</summary>
    public async Task<string> CreateIssueAsync(string title, string body, CancellationToken ct = default)
    {
        if (!HasToken)
            throw new InvalidOperationException(
                "کلیدِ GitHub تنظیم نشده است — GITHUB_TOKEN را در تنظیماتِ سرور (بخشِ GitHub) قرار دهید.");

        var payload = JsonSerializer.Serialize(new { title, body }, JsonOpts);
        using var req = new HttpRequestMessage(HttpMethod.Post, $"/repos/{_owner}/{_repo}/issues")
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json"),
        };
        req.Headers.Authorization = new("Bearer", _token);

        using var res = await _http.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode)
        {
            var detail = await res.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("GitHub issue: HTTP {Status} — {Detail}", (int)res.StatusCode, detail[..Math.Min(detail.Length, 300)]);
            throw new InvalidOperationException($"ثبتِ Issue در GitHub ناموفق بود (HTTP {(int)res.StatusCode}).");
        }

        await using var stream = await res.Content.ReadAsStreamAsync(ct);
        var created = await JsonSerializer.DeserializeAsync<GhIssue>(stream, JsonOpts, ct);
        return created?.HtmlUrl ?? $"https://github.com/{_owner}/{_repo}/issues";
    }

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private sealed class GhRelease
    {
        public string TagName { get; set; } = "";
        public string? Name { get; set; }
        public string? Body { get; set; }
        public DateTimeOffset? PublishedAt { get; set; }
        public string? HtmlUrl { get; set; }
    }

    private sealed class GhIssue
    {
        public string? HtmlUrl { get; set; }
    }
}
