using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Domain.Entities.Support;
using SamaHesab.Domain.Enums;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Support.Commands;

/// <summary>
/// 🆘 HC-5 — همگام‌سازیِ یادداشت‌های نسخه از وردپرس به کَشِ محلی.
/// اگر سرور در دسترس نباشد، نسخهٔ کَش‌شدهٔ محلی برگردانده می‌شود (آفلاین‌محور).
/// </summary>
public record SyncReleaseNotesCommand : IRequest<IReadOnlyList<ReleaseDto>>;

public class SyncReleaseNotesCommandHandler : IRequestHandler<SyncReleaseNotesCommand, IReadOnlyList<ReleaseDto>>
{
    private readonly IRepository<ReleaseNote> _repo;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;
    private readonly ISupportApiClient _api;

    public SyncReleaseNotesCommandHandler(IRepository<ReleaseNote> repo, IUnitOfWork uow,
        ICurrentUserService user, ISupportApiClient api)
    { _repo = repo; _uow = uow; _user = user; _api = api; }

    public async Task<IReadOnlyList<ReleaseDto>> Handle(SyncReleaseNotesCommand req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        if (_api.IsConfigured)
        {
            var fetched = await _api.GetReleasesAsync(ct);
            if (fetched.Succeeded && fetched.Value is { Count: > 0 })
            {
                var existing = await _repo.FindAsync(r => r.CompanyId == companyId, ct);
                foreach (var dto in fetched.Value)
                {
                    var row = existing.FirstOrDefault(r => r.RemoteId == dto.RemoteId);
                    if (row is null)
                        await _repo.AddAsync(ReleaseNote.Create(companyId, dto.RemoteId, dto.Version,
                            dto.Highlights, dto.BugFixes, dto.KnownIssues, dto.PublishedAt, dto.IsCurrent), ct);
                    else { row.Refresh(dto.Version, dto.Highlights, dto.BugFixes, dto.KnownIssues, dto.PublishedAt, dto.IsCurrent); _repo.Update(row); }
                }
                await _uow.SaveChangesAsync(ct);
            }
        }

        var cached = await _repo.FindAsync(r => r.CompanyId == companyId, ct);
        return cached
            .OrderByDescending(r => r.IsCurrent).ThenByDescending(r => r.PublishedAt)
            .Select(r => new ReleaseDto(r.RemoteId, r.Version, r.Highlights, r.BugFixes, r.KnownIssues, r.PublishedAt, r.IsCurrent))
            .ToList();
    }
}

/// <summary>
/// 🆘 HC-5 — همگام‌سازیِ دانشنامه از وردپرس به کَشِ محلی + جست‌وجو.
/// آفلاین → جست‌وجو روی کَشِ محلی.
/// </summary>
public record SyncKnowledgeArticlesCommand(string? Search) : IRequest<IReadOnlyList<ArticleDto>>;

public class SyncKnowledgeArticlesCommandHandler : IRequestHandler<SyncKnowledgeArticlesCommand, IReadOnlyList<ArticleDto>>
{
    private readonly IRepository<KnowledgeArticle> _repo;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;
    private readonly ISupportApiClient _api;

    public SyncKnowledgeArticlesCommandHandler(IRepository<KnowledgeArticle> repo, IUnitOfWork uow,
        ICurrentUserService user, ISupportApiClient api)
    { _repo = repo; _uow = uow; _user = user; _api = api; }

    public async Task<IReadOnlyList<ArticleDto>> Handle(SyncKnowledgeArticlesCommand req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        if (_api.IsConfigured)
        {
            var fetched = await _api.GetArticlesAsync(req.Search, ct);
            if (fetched.Succeeded && fetched.Value is { Count: > 0 })
            {
                var existing = await _repo.FindAsync(a => a.CompanyId == companyId, ct);
                foreach (var dto in fetched.Value)
                {
                    var cat = (SupportCategory)dto.Category;
                    var row = existing.FirstOrDefault(a => a.RemoteId == dto.RemoteId);
                    if (row is null)
                        await _repo.AddAsync(KnowledgeArticle.Create(companyId, dto.RemoteId, dto.Title,
                            cat, dto.Summary, dto.Body, dto.Url, dto.Kind, dto.PublishedAt), ct);
                    else { row.Refresh(dto.Title, cat, dto.Summary, dto.Body, dto.Url, dto.Kind, dto.PublishedAt); _repo.Update(row); }
                }
                await _uow.SaveChangesAsync(ct);
            }
        }

        var cached = await _repo.FindAsync(a => a.CompanyId == companyId, ct);
        IEnumerable<KnowledgeArticle> q = cached;
        if (!string.IsNullOrWhiteSpace(req.Search))
        {
            var s = req.Search.Trim();
            q = cached.Where(a => (a.Title?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false)
                               || (a.Summary?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false));
        }
        return q.OrderByDescending(a => a.PublishedAt)
            .Select(a => new ArticleDto(a.RemoteId, a.Title, (int)a.Category, a.Summary, a.Body, a.Url, a.Kind, a.PublishedAt))
            .ToList();
    }
}
