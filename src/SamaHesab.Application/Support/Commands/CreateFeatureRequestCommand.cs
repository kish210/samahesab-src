using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Entities.Support;
using SamaHesab.Domain.Enums;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Support.Commands;

/// <summary>🆘 HC-4 — ثبتِ درخواستِ قابلیت. محلی ذخیره + همگام/صف (مثلِ گزارشِ باگ).</summary>
public record CreateFeatureRequestCommand(
    string Title, string Description, string? BusinessBenefit, FeaturePriority Priority, string? AttachmentPath)
    : IRequest<Result<BugReportResult>>;

public class CreateFeatureRequestCommandHandler : IRequestHandler<CreateFeatureRequestCommand, Result<BugReportResult>>
{
    private readonly IRepository<FeatureRequest> _repo;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;
    private readonly ISupportApiClient _api;

    public CreateFeatureRequestCommandHandler(IRepository<FeatureRequest> repo, IUnitOfWork uow,
        ICurrentUserService user, ISupportApiClient api)
    { _repo = repo; _uow = uow; _user = user; _api = api; }

    public async Task<Result<BugReportResult>> Handle(CreateFeatureRequestCommand req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Title)) return Result<BugReportResult>.Failure("عنوانِ درخواست الزامی است.");
        if (string.IsNullOrWhiteSpace(req.Description)) return Result<BugReportResult>.Failure("شرحِ درخواست الزامی است.");

        var f = FeatureRequest.Create(_user.CompanyId ?? 1, req.Title, req.Description,
            req.BusinessBenefit, req.Priority, req.AttachmentPath);

        var message = "درخواست به‌صورتِ محلی ثبت شد و هنگامِ اتصال ارسال می‌شود.";
        if (_api.IsConfigured)
        {
            var dto = new FeatureSubmitDto(req.Title, req.Description, req.BusinessBenefit, (int)req.Priority, null, null);
            var sent = await _api.SubmitFeatureAsync(dto, ct);
            if (sent.Succeeded && sent.Value is { Length: > 0 })
            {
                f.MarkSynced(sent.Value);
                message = "درخواست با موفقیت ارسال شد. کدِ پیگیری: " + sent.Value;
            }
            else f.MarkQueued();
        }
        else f.MarkQueued();

        await _repo.AddAsync(f, ct);
        await _uow.SaveChangesAsync(ct);
        return Result<BugReportResult>.Success(new BugReportResult(f.Id, f.Sync, message));
    }
}
