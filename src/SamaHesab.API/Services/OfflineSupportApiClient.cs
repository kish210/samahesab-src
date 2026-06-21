using SamaHesab.Application.Common.Models;
using SamaHesab.Application.Support;

namespace SamaHesab.API.Services;

/// <summary>
/// پیاده‌سازیِ no-op از ISupportApiClient برای میزبانِ API.
/// همگام‌سازی با سرورِ پشتیبانی یک قابلیتِ سمتِ کلاینتِ دسکتاپ است؛ میزبانِ API به آن نیاز ندارد،
/// ولی هندلرهای MediatRِ پشتیبانی به این وابستگی نیاز دارند تا DI معتبر بماند (رفعِ ValidateOnBuild).
/// «پیکربندی‌نشده» برمی‌گرداند تا فراخواننده در صورتِ نیاز store-and-forward کند.
/// </summary>
public sealed class OfflineSupportApiClient : ISupportApiClient
{
    public bool IsConfigured => false;
    private const string Msg = "سرویسِ پشتیبانی روی میزبانِ API در دسترس نیست.";

    public Task<Result<string>> SubmitBugAsync(BugSubmitDto dto, CancellationToken ct = default)
        => Task.FromResult(Result<string>.Failure(Msg));
    public Task<Result<string>> SubmitFeatureAsync(FeatureSubmitDto dto, CancellationToken ct = default)
        => Task.FromResult(Result<string>.Failure(Msg));
    public Task<Result<string>> SubmitTicketAsync(TicketSubmitDto dto, CancellationToken ct = default)
        => Task.FromResult(Result<string>.Failure(Msg));
    public Task<Result<IReadOnlyList<ReleaseDto>>> GetReleasesAsync(CancellationToken ct = default)
        => Task.FromResult(Result<IReadOnlyList<ReleaseDto>>.Success(System.Array.Empty<ReleaseDto>()));
    public Task<Result<IReadOnlyList<ArticleDto>>> GetArticlesAsync(string? search, CancellationToken ct = default)
        => Task.FromResult(Result<IReadOnlyList<ArticleDto>>.Success(System.Array.Empty<ArticleDto>()));
    public Task<Result<RemoteStatusDto>> GetStatusAsync(string remoteId, CancellationToken ct = default)
        => Task.FromResult(Result<RemoteStatusDto>.Failure(Msg));
    public Task<Result<string>> SubmitRemoteSessionAsync(RemoteSessionSubmitDto dto, CancellationToken ct = default)
        => Task.FromResult(Result<string>.Failure(Msg));
    public Task<Result<InstallStatusDto>> RegisterInstallAsync(InstallInfo info, CancellationToken ct = default)
        => Task.FromResult(Result<InstallStatusDto>.Failure(Msg));
}
