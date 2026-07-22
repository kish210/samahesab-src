using System.Net.Http.Json;
using SamaHesab.Application.Common.Models;
using SamaHesab.Application.Support;

namespace SamaHesab.API.Services;

/// <summary>
/// U-WEB-SUPPORT — پیاده‌سازیِ ISupportApiClient برایِ میزبانِ API، با کانفیگ از appsettings.json
/// (بخشِ "Support": BaseUrl/CustomerId/ApiKey/LicenseId) — نه از AppSettingsStoreِ محلیِ دسکتاپ
/// (آن فایل per-machine است و روی سرور معنا ندارد). عیناً همان پروتکل/مسیرهایِ
/// SamaHesab.WPF/Services/SupportApiClient.cs (پلاگینِ وردپرسِ kishwifi.com). اگر کانفیگ‌نشده
/// باشد، رفتارش دقیقاً مثلِ OfflineSupportApiClientِ قبلی است (که این کلاس جایگزینش شد).
/// </summary>
public sealed class ConfiguredSupportApiClient : ISupportApiClient
{
    private const string ApiBase = "/wp-json/samahesab/v1";
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(20) };
    private readonly SupportApiOptions _options;

    public ConfiguredSupportApiClient(IConfiguration config)
    {
        var s = config.GetSection("Support");
        _options = new SupportApiOptions(
            s["BaseUrl"]?.TrimEnd('/') ?? "", s["CustomerId"] ?? "", s["ApiKey"] ?? "", s["LicenseId"] ?? "");
    }

    public bool IsConfigured => _options.IsValid;

    private HttpRequestMessage Build(HttpMethod method, string path, object? body)
    {
        var req = new HttpRequestMessage(method, $"{_options.BaseUrl}{ApiBase}{path}");
        req.Headers.TryAddWithoutValidation("X-SamaHesab-ApiKey", _options.ApiKey);
        req.Headers.TryAddWithoutValidation("X-SamaHesab-Customer", _options.CustomerId);
        req.Headers.TryAddWithoutValidation("X-SamaHesab-License", _options.LicenseId);
        if (body is not null) req.Content = JsonContent.Create(body);
        return req;
    }

    private const string NotConfiguredMsg = "سرورِ پشتیبانی پیکربندی نشده است.";

    private async Task<Result<string>> PostForIdAsync(string path, object body, CancellationToken ct)
    {
        if (!IsConfigured) return Result<string>.Failure(NotConfiguredMsg);
        try
        {
            using var resp = await _http.SendAsync(Build(HttpMethod.Post, path, body), ct);
            if (!resp.IsSuccessStatusCode) return Result<string>.Failure($"سرورِ پشتیبانی خطا داد ({(int)resp.StatusCode}).");
            var dto = await resp.Content.ReadFromJsonAsync<SubmitResponse>(cancellationToken: ct);
            return dto?.Id is { Length: > 0 } ? Result<string>.Success(dto.Id) : Result<string>.Failure("پاسخِ نامعتبر از سرورِ پشتیبانی.");
        }
        catch (Exception ex) { return Result<string>.Failure("اتصال به سرورِ پشتیبانی ناموفق بود: " + ex.Message); }
    }

    public Task<Result<string>> SubmitBugAsync(BugSubmitDto dto, CancellationToken ct = default) => PostForIdAsync("/bug", dto, ct);
    public Task<Result<string>> SubmitFeatureAsync(FeatureSubmitDto dto, CancellationToken ct = default) => PostForIdAsync("/feature", dto, ct);
    public Task<Result<string>> SubmitTicketAsync(TicketSubmitDto dto, CancellationToken ct = default) => PostForIdAsync("/ticket", dto, ct);
    public Task<Result<string>> SubmitRemoteSessionAsync(RemoteSessionSubmitDto dto, CancellationToken ct = default) => PostForIdAsync("/remote", dto, ct);

    public async Task<Result<InstallStatusDto>> RegisterInstallAsync(InstallInfo info, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl)) return Result<InstallStatusDto>.Failure("آدرسِ سرورِ پشتیبانی تنظیم نشده است.");
        try
        {
            var body = new { machine_id = info.MachineId, company = info.Company, business_type = info.BusinessType, version = info.Version };
            using var resp = await _http.SendAsync(Build(HttpMethod.Post, "/register", body), ct);
            if (!resp.IsSuccessStatusCode) return Result<InstallStatusDto>.Failure($"خطای سرور ({(int)resp.StatusCode}).");
            var d = await resp.Content.ReadFromJsonAsync<RegisterResponse>(cancellationToken: ct);
            if (d is null) return Result<InstallStatusDto>.Failure("پاسخِ نامعتبر از سرور.");
            return Result<InstallStatusDto>.Success(new InstallStatusDto(d.approved, d.valid, d.expired, d.api_key, d.license_id, d.expiry, d.days_remaining, d.doc_limit));
        }
        catch (Exception ex) { return Result<InstallStatusDto>.Failure("اتصال ناموفق: " + ex.Message); }
    }

    private sealed record RegisterResponse(bool ok, bool approved, bool valid, bool expired,
        string? api_key, string? license_id, string? expiry, int? days_remaining, int doc_limit);

    public async Task<Result<IReadOnlyList<ReleaseDto>>> GetReleasesAsync(CancellationToken ct = default)
    {
        if (!IsConfigured) return Result<IReadOnlyList<ReleaseDto>>.Success(Array.Empty<ReleaseDto>());
        try
        {
            using var resp = await _http.SendAsync(Build(HttpMethod.Get, "/releases", null), ct);
            if (!resp.IsSuccessStatusCode) return Result<IReadOnlyList<ReleaseDto>>.Failure($"خطای سرور ({(int)resp.StatusCode}).");
            var list = await resp.Content.ReadFromJsonAsync<List<ReleaseDto>>(cancellationToken: ct) ?? new();
            return Result<IReadOnlyList<ReleaseDto>>.Success(list);
        }
        catch (Exception ex) { return Result<IReadOnlyList<ReleaseDto>>.Failure("اتصال ناموفق: " + ex.Message); }
    }

    public async Task<Result<IReadOnlyList<ArticleDto>>> GetArticlesAsync(string? search, CancellationToken ct = default)
    {
        if (!IsConfigured) return Result<IReadOnlyList<ArticleDto>>.Success(Array.Empty<ArticleDto>());
        try
        {
            var q = string.IsNullOrWhiteSpace(search) ? "" : "?search=" + Uri.EscapeDataString(search);
            using var resp = await _http.SendAsync(Build(HttpMethod.Get, "/articles" + q, null), ct);
            if (!resp.IsSuccessStatusCode) return Result<IReadOnlyList<ArticleDto>>.Failure($"خطای سرور ({(int)resp.StatusCode}).");
            var list = await resp.Content.ReadFromJsonAsync<List<ArticleDto>>(cancellationToken: ct) ?? new();
            return Result<IReadOnlyList<ArticleDto>>.Success(list);
        }
        catch (Exception ex) { return Result<IReadOnlyList<ArticleDto>>.Failure("اتصال ناموفق: " + ex.Message); }
    }

    public async Task<Result<RemoteStatusDto>> GetStatusAsync(string remoteId, CancellationToken ct = default)
    {
        if (!IsConfigured) return Result<RemoteStatusDto>.Failure(NotConfiguredMsg);
        try
        {
            using var resp = await _http.SendAsync(Build(HttpMethod.Get, "/status/" + Uri.EscapeDataString(remoteId), null), ct);
            if (!resp.IsSuccessStatusCode) return Result<RemoteStatusDto>.Failure($"خطای سرور ({(int)resp.StatusCode}).");
            var dto = await resp.Content.ReadFromJsonAsync<RemoteStatusDto>(cancellationToken: ct);
            return dto is null ? Result<RemoteStatusDto>.Failure("پاسخِ نامعتبر.") : Result<RemoteStatusDto>.Success(dto);
        }
        catch (Exception ex) { return Result<RemoteStatusDto>.Failure("اتصال ناموفق: " + ex.Message); }
    }

    private sealed record SubmitResponse(string Id);
}
