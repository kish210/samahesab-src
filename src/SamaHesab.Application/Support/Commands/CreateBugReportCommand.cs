using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Entities.Support;
using SamaHesab.Domain.Enums;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Support.Commands;

/// <summary>
/// 🆘 HC-3 — ثبتِ گزارشِ باگ. محلی ذخیره می‌شود؛ سپس اگر سرورِ پشتیبانی پیکربندی شده باشد
/// همگام (Sync) می‌شود، وگرنه برای ارسالِ بعدی صف (Queued) می‌شود. عکسِ تشخیصی فقط دادهٔ فنی است.
/// </summary>
public record CreateBugReportCommand(
    string Title, string Description, BugSeverity Severity, SupportCategory Category,
    string? ExpectedResult, string? ActualResult, string? StepsToReproduce,
    string? DiagnosticsJson, string? ScreenName, string? AttachmentPath)
    : IRequest<Result<BugReportResult>>;

/// <summary>نتیجهٔ ثبت: شناسهٔ محلی + وضعیتِ همگام‌سازی + پیامِ کاربر-پسند.</summary>
public record BugReportResult(int LocalId, SyncState Sync, string Message);

public class CreateBugReportCommandHandler : IRequestHandler<CreateBugReportCommand, Result<BugReportResult>>
{
    private readonly IRepository<BugReport> _repo;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;
    private readonly ISupportApiClient _api;

    public CreateBugReportCommandHandler(IRepository<BugReport> repo, IUnitOfWork uow,
        ICurrentUserService user, ISupportApiClient api)
    { _repo = repo; _uow = uow; _user = user; _api = api; }

    public async Task<Result<BugReportResult>> Handle(CreateBugReportCommand req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Title)) return Result<BugReportResult>.Failure("عنوانِ گزارش الزامی است.");
        if (string.IsNullOrWhiteSpace(req.Description)) return Result<BugReportResult>.Failure("شرحِ مشکل الزامی است.");

        var companyId = _user.CompanyId ?? 1;
        var bug = BugReport.Create(companyId, req.Title, req.Description, req.Severity, req.Category,
            req.ExpectedResult, req.ActualResult, req.StepsToReproduce,
            req.DiagnosticsJson, req.ScreenName, req.AttachmentPath);

        // تلاش برای ارسالِ فوری؛ در صورتِ آفلاین/خطا → صفِ محلی (store-and-forward).
        var message = "گزارش به‌صورتِ محلی ثبت شد و هنگامِ اتصال ارسال می‌شود.";
        if (_api.IsConfigured)
        {
            var dto = new BugSubmitDto(req.Title, req.Description, (int)req.Severity, (int)req.Category,
                req.ExpectedResult, req.ActualResult, req.StepsToReproduce, req.DiagnosticsJson, req.ScreenName, null, null);
            var sent = await _api.SubmitBugAsync(dto, ct);
            if (sent.Succeeded && sent.Value is { Length: > 0 })
            {
                bug.MarkSynced(sent.Value);
                message = "گزارش با موفقیت برای تیمِ پشتیبانی ارسال شد. کدِ پیگیری: " + sent.Value;
            }
            else bug.MarkQueued();
        }
        else bug.MarkQueued();

        await _repo.AddAsync(bug, ct);
        await _uow.SaveChangesAsync(ct);

        return Result<BugReportResult>.Success(new BugReportResult(bug.Id, bug.Sync, message));
    }
}
