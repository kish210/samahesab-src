using System.Net.Http;
using System.Net.Http.Json;
using SamaHesab.Application.Common.Models;
using SamaHesab.Application.Support;

namespace SamaHesab.WPF.Services;

/// <summary>
/// 🆘 HC-2 — پیاده‌سازیِ کلاینتِ پشتیبانی روی REST API وردپرس (پلاگینِ HC-WP).
/// مسیرها: <c>{BaseUrl}/wp-json/samahesab/v1/{...}</c>. احراز با هدرِ کلید-API.
/// آفلاین‌محور: اگر پیکربندی نشده/سرور قطع باشد، <see cref="Result"/>ِ ناموفقِ گویا برمی‌گرداند
/// (فراخواننده رکورد را محلی صف می‌کند). فقط دادهٔ فنی/پشتیبانی ارسال می‌شود.
/// </summary>
public sealed class SupportApiClient : ISupportApiClient
{
    private const string ApiBase = "/wp-json/samahesab/v1";
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(20) };

    private SupportApiOptions Options()
    {
        var s = AppSettingsStore.GetSupport();
        return new SupportApiOptions(s.BaseUrl?.TrimEnd('/') ?? "", s.CustomerId ?? "", s.ApiKey ?? "", s.LicenseId ?? "");
    }

    public bool IsConfigured => Options().IsValid;

    private HttpRequestMessage Build(HttpMethod method, string path, object? body, SupportApiOptions o)
    {
        var req = new HttpRequestMessage(method, $"{o.BaseUrl}{ApiBase}{path}");
        req.Headers.TryAddWithoutValidation("X-SamaHesab-ApiKey", o.ApiKey);
        req.Headers.TryAddWithoutValidation("X-SamaHesab-Customer", o.CustomerId);
        req.Headers.TryAddWithoutValidation("X-SamaHesab-License", o.LicenseId);
        if (body is not null) req.Content = JsonContent.Create(body);
        return req;
    }

    private async Task<Result<string>> PostForIdAsync(string path, object body, CancellationToken ct)
    {
        var o = Options();
        if (!o.IsValid) return Result<string>.Failure("سرورِ پشتیبانی پیکربندی نشده است (آفلاین — صفِ محلی).");
        try
        {
            using var resp = await _http.SendAsync(Build(HttpMethod.Post, path, body, o), ct);
            if (!resp.IsSuccessStatusCode)
                return Result<string>.Failure($"سرورِ پشتیبانی خطا داد ({(int)resp.StatusCode}).");
            var dto = await resp.Content.ReadFromJsonAsync<SubmitResponse>(cancellationToken: ct);
            return dto?.Id is { Length: > 0 }
                ? Result<string>.Success(dto.Id)
                : Result<string>.Failure("پاسخِ نامعتبر از سرورِ پشتیبانی.");
        }
        catch (Exception ex)
        {
            return Result<string>.Failure("اتصال به سرورِ پشتیبانی ناموفق بود (آفلاین — صفِ محلی): " + ex.Message);
        }
    }

    public Task<Result<string>> SubmitBugAsync(BugSubmitDto dto, CancellationToken ct = default)
        => PostForIdAsync("/bug", dto, ct);

    public Task<Result<string>> SubmitFeatureAsync(FeatureSubmitDto dto, CancellationToken ct = default)
        => PostForIdAsync("/feature", dto, ct);

    public Task<Result<string>> SubmitTicketAsync(TicketSubmitDto dto, CancellationToken ct = default)
        => PostForIdAsync("/ticket", dto, ct);

    public Task<Result<string>> SubmitRemoteSessionAsync(RemoteSessionSubmitDto dto, CancellationToken ct = default)
        => PostForIdAsync("/remote", dto, ct);   // 🆘 HC-6b — ثبتِ نشستِ ریموت روی vendor

    /// <summary>🖥 اعلامِ نصب به سایت + گرفتنِ وضعیتِ تأیید/لایسنس. endpointِ باز — فقط BaseUrl لازم است (نه کلید).</summary>
    public async Task<Result<InstallStatusDto>> RegisterInstallAsync(InstallInfo info, CancellationToken ct = default)
    {
        var o = Options();
        if (string.IsNullOrWhiteSpace(o.BaseUrl))
            return Result<InstallStatusDto>.Failure("آدرسِ سرورِ پشتیبانی تنظیم نشده است.");
        try
        {
            var body = new { machine_id = info.MachineId, company = info.Company, business_type = info.BusinessType, version = info.Version };
            using var resp = await _http.SendAsync(Build(HttpMethod.Post, "/register", body, o), ct);
            if (!resp.IsSuccessStatusCode)
                return Result<InstallStatusDto>.Failure($"خطای سرور ({(int)resp.StatusCode}).");
            var d = await resp.Content.ReadFromJsonAsync<RegisterResponse>(cancellationToken: ct);
            if (d is null) return Result<InstallStatusDto>.Failure("پاسخِ نامعتبر از سرور.");
            return Result<InstallStatusDto>.Success(new InstallStatusDto(
                d.approved, d.valid, d.expired, d.api_key, d.license_id, d.expiry, d.days_remaining, d.doc_limit));
        }
        catch (Exception ex) { return Result<InstallStatusDto>.Failure("اتصال ناموفق: " + ex.Message); }
    }

    private sealed record RegisterResponse(bool ok, bool approved, bool valid, bool expired,
        string? api_key, string? license_id, string? expiry, int? days_remaining, int doc_limit);

    public async Task<Result<IReadOnlyList<ReleaseDto>>> GetReleasesAsync(CancellationToken ct = default)
    {
        var o = Options();
        if (!o.IsValid) return Result<IReadOnlyList<ReleaseDto>>.Failure("سرورِ پشتیبانی پیکربندی نشده است.");
        try
        {
            using var resp = await _http.SendAsync(Build(HttpMethod.Get, "/releases", null, o), ct);
            if (!resp.IsSuccessStatusCode) return Result<IReadOnlyList<ReleaseDto>>.Failure($"خطای سرور ({(int)resp.StatusCode}).");
            var list = await resp.Content.ReadFromJsonAsync<List<ReleaseDto>>(cancellationToken: ct) ?? new();
            return Result<IReadOnlyList<ReleaseDto>>.Success(list);
        }
        catch (Exception ex) { return Result<IReadOnlyList<ReleaseDto>>.Failure("اتصال ناموفق: " + ex.Message); }
    }

    public async Task<Result<IReadOnlyList<ArticleDto>>> GetArticlesAsync(string? search, CancellationToken ct = default)
    {
        var o = Options();
        if (!o.IsValid) return Result<IReadOnlyList<ArticleDto>>.Failure("سرورِ پشتیبانی پیکربندی نشده است.");
        try
        {
            var q = string.IsNullOrWhiteSpace(search) ? "" : "?search=" + Uri.EscapeDataString(search);
            using var resp = await _http.SendAsync(Build(HttpMethod.Get, "/articles" + q, null, o), ct);
            if (!resp.IsSuccessStatusCode) return Result<IReadOnlyList<ArticleDto>>.Failure($"خطای سرور ({(int)resp.StatusCode}).");
            var list = await resp.Content.ReadFromJsonAsync<List<ArticleDto>>(cancellationToken: ct) ?? new();
            return Result<IReadOnlyList<ArticleDto>>.Success(list);
        }
        catch (Exception ex) { return Result<IReadOnlyList<ArticleDto>>.Failure("اتصال ناموفق: " + ex.Message); }
    }

    public async Task<Result<RemoteStatusDto>> GetStatusAsync(string remoteId, CancellationToken ct = default)
    {
        var o = Options();
        if (!o.IsValid) return Result<RemoteStatusDto>.Failure("سرورِ پشتیبانی پیکربندی نشده است.");
        try
        {
            using var resp = await _http.SendAsync(Build(HttpMethod.Get, "/status/" + Uri.EscapeDataString(remoteId), null, o), ct);
            if (!resp.IsSuccessStatusCode) return Result<RemoteStatusDto>.Failure($"خطای سرور ({(int)resp.StatusCode}).");
            var dto = await resp.Content.ReadFromJsonAsync<RemoteStatusDto>(cancellationToken: ct);
            return dto is null ? Result<RemoteStatusDto>.Failure("پاسخِ نامعتبر.") : Result<RemoteStatusDto>.Success(dto);
        }
        catch (Exception ex) { return Result<RemoteStatusDto>.Failure("اتصال ناموفق: " + ex.Message); }
    }

    private sealed record SubmitResponse(string Id);
}
