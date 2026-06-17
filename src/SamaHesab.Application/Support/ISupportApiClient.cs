using SamaHesab.Application.Common.Models;

namespace SamaHesab.Application.Support;

/// <summary>
/// 🆘 HC-2 — قراردادِ کلاینتِ همگام‌سازی با سرورِ پشتیبانیِ وردپرسِ kishwifi.com.
/// احرازِ هویت با «کلید-API» (هر نصبِ ERP: CustomerId/ApiKey/LicenseId).
/// آفلاین‌محور: اگر پیکربندی نشده یا سرور در دسترس نباشد، فراخوان با خطای گویا برمی‌گردد
/// و فراخواننده رکورد را به‌صورتِ محلی صف می‌کند (store-and-forward).
/// <para>⚠️ فقط Diagnostics/Log/خطا/محتوای واردشدهٔ کاربر ارسال می‌شود — هیچ دادهٔ مالی.</para>
/// </summary>
public interface ISupportApiClient
{
    /// <summary>آیا کلید-API و آدرسِ سرور تنظیم شده‌اند؟</summary>
    bool IsConfigured { get; }

    Task<Result<string>> SubmitBugAsync(BugSubmitDto dto, CancellationToken ct = default);
    Task<Result<string>> SubmitFeatureAsync(FeatureSubmitDto dto, CancellationToken ct = default);
    Task<Result<string>> SubmitTicketAsync(TicketSubmitDto dto, CancellationToken ct = default);

    Task<Result<IReadOnlyList<ReleaseDto>>> GetReleasesAsync(CancellationToken ct = default);
    Task<Result<IReadOnlyList<ArticleDto>>> GetArticlesAsync(string? search, CancellationToken ct = default);

    /// <summary>وضعیتِ یک تیکت/گزارشِ ارسال‌شده را از سرور می‌خواند (برای «درخواست‌های من»).</summary>
    Task<Result<RemoteStatusDto>> GetStatusAsync(string remoteId, CancellationToken ct = default);
}

/// <summary>کلید-APIِ نصبِ ERP (از تنظیماتِ پشتیبانی خوانده می‌شود).</summary>
public sealed record SupportApiOptions(string BaseUrl, string CustomerId, string ApiKey, string LicenseId)
{
    public bool IsValid =>
        !string.IsNullOrWhiteSpace(BaseUrl) &&
        !string.IsNullOrWhiteSpace(CustomerId) &&
        !string.IsNullOrWhiteSpace(ApiKey);
}

// ── DTOهای ارسال ──
public sealed record BugSubmitDto(
    string Title, string Description, int Severity, int Category,
    string? ExpectedResult, string? ActualResult, string? StepsToReproduce,
    string? DiagnosticsJson, string? ScreenName, string? AttachmentBase64, string? AttachmentName);

public sealed record FeatureSubmitDto(
    string Title, string Description, string? BusinessBenefit, int Priority,
    string? AttachmentBase64, string? AttachmentName);

public sealed record TicketSubmitDto(string Subject, string Body, int Category);

// ── DTOهای دریافت ──
public sealed record ReleaseDto(string RemoteId, string Version, string? Highlights,
    string? BugFixes, string? KnownIssues, DateTime? PublishedAt, bool IsCurrent);

public sealed record ArticleDto(string RemoteId, string Title, int Category,
    string? Summary, string? Body, string? Url, string Kind, DateTime? PublishedAt);

public sealed record RemoteStatusDto(string RemoteId, int Status, string? StatusText, DateTime? UpdatedAt);
