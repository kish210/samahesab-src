using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Entities.Documents;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Documents;

// ── Portable package (DT-5) — فرمتِ import/export قالب (.shtpl JSON، پایهٔ مارکت‌پلیس) ──
public record DocumentTemplatePackage(string Format, string DocumentType, string Name, string PaperSize,
    string? HeaderHtml, string BodyHtml, string? FooterHtml)
{
    public const string CurrentFormat = "shtpl-v1";
}

// ── List (per document type) ─────────────────────────────────────────────────
public record DocumentTemplateListDto(int Id, string Name, string PaperSize, bool IsDefault, bool IsActive, bool IsSystem);
public record GetDocumentTemplatesQuery(string DocumentType) : IRequest<List<DocumentTemplateListDto>>;

public class GetDocumentTemplatesQueryHandler : IRequestHandler<GetDocumentTemplatesQuery, List<DocumentTemplateListDto>>
{
    private readonly IRepository<DocumentTemplate> _repo;
    private readonly ICurrentUserService _user;
    public GetDocumentTemplatesQueryHandler(IRepository<DocumentTemplate> repo, ICurrentUserService user)
    { _repo = repo; _user = user; }

    public async Task<List<DocumentTemplateListDto>> Handle(GetDocumentTemplatesQuery req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        var list = await _repo.FindAsync(t => t.CompanyId == companyId && t.DocumentType == req.DocumentType && t.IsActive, ct);
        return list.OrderByDescending(t => t.IsDefault).ThenBy(t => t.Name)
            .Select(t => new DocumentTemplateListDto(t.Id, t.Name, t.PaperSize, t.IsDefault, t.IsActive, t.IsSystem))
            .ToList();
    }
}

// ── Full (for render/edit) ───────────────────────────────────────────────────
public record DocumentTemplateDto(int Id, string DocumentType, string Name, string PaperSize,
    string? HeaderHtml, string BodyHtml, string? FooterHtml);
public record GetDocumentTemplateQuery(int Id) : IRequest<DocumentTemplateDto?>;

public class GetDocumentTemplateQueryHandler : IRequestHandler<GetDocumentTemplateQuery, DocumentTemplateDto?>
{
    private readonly IRepository<DocumentTemplate> _repo;
    public GetDocumentTemplateQueryHandler(IRepository<DocumentTemplate> repo) => _repo = repo;

    public async Task<DocumentTemplateDto?> Handle(GetDocumentTemplateQuery req, CancellationToken ct)
    {
        var t = await _repo.GetByIdAsync(req.Id, ct);
        return t is null ? null
            : new DocumentTemplateDto(t.Id, t.DocumentType, t.Name, t.PaperSize, t.HeaderHtml, t.BodyHtml, t.FooterHtml);
    }
}

// ── Save (create/update) ─────────────────────────────────────────────────────
public record SaveDocumentTemplateCommand(int? Id, string DocumentType, string Name, string PaperSize,
    string? HeaderHtml, string BodyHtml, string? FooterHtml, bool IsDefault = false) : IRequest<Result<int>>;

public class SaveDocumentTemplateCommandHandler : IRequestHandler<SaveDocumentTemplateCommand, Result<int>>
{
    private readonly IRepository<DocumentTemplate> _repo;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;
    public SaveDocumentTemplateCommandHandler(IRepository<DocumentTemplate> repo, IUnitOfWork uow, ICurrentUserService user)
    { _repo = repo; _uow = uow; _user = user; }

    public async Task<Result<int>> Handle(SaveDocumentTemplateCommand req, CancellationToken ct)
    {
        try
        {
            DocumentTemplate entity;
            if (req.Id is int id && id > 0)
            {
                entity = await _repo.GetByIdAsync(id, ct) ?? throw new InvalidOperationException("قالب یافت نشد.");
                entity.Update(req.Name, req.BodyHtml, req.PaperSize, req.HeaderHtml, req.FooterHtml);
                if (req.IsDefault) entity.SetDefault(true);
                _repo.Update(entity);
            }
            else
            {
                entity = DocumentTemplate.Create(_user.CompanyId ?? 1, req.DocumentType, req.Name,
                    req.BodyHtml, req.PaperSize, req.HeaderHtml, req.FooterHtml, req.IsDefault);
                await _repo.AddAsync(entity, ct);
            }
            await _uow.SaveChangesAsync(ct);
            return Result<int>.Success(entity.Id);
        }
        catch (Exception ex) { return Result<int>.Failure(ex.GetBaseException().Message); }
    }
}

// ── Set default (DT-10 — یک قالبِ پیش‌فرض برای هر نوعِ سند؛ بقیه unset می‌شوند) ─────
public record SetDefaultTemplateCommand(int Id) : IRequest<Result>;

public class SetDefaultTemplateCommandHandler : IRequestHandler<SetDefaultTemplateCommand, Result>
{
    private readonly IRepository<DocumentTemplate> _repo;
    private readonly IUnitOfWork _uow;
    public SetDefaultTemplateCommandHandler(IRepository<DocumentTemplate> repo, IUnitOfWork uow)
    { _repo = repo; _uow = uow; }

    public async Task<Result> Handle(SetDefaultTemplateCommand req, CancellationToken ct)
    {
        var target = await _repo.GetByIdAsync(req.Id, ct);
        if (target is null) return Result.Failure("قالب یافت نشد.");
        // همهٔ قالب‌های هم‌شرکت/هم‌نوع را unset کن، سپس فقط هدف را پیش‌فرض کن.
        var siblings = await _repo.FindAsync(
            t => t.CompanyId == target.CompanyId && t.DocumentType == target.DocumentType, ct);
        foreach (var s in siblings)
        {
            var shouldBe = s.Id == target.Id;
            if (s.IsDefault != shouldBe) { s.SetDefault(shouldBe); _repo.Update(s); }
        }
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}

// ── Delete (block system templates) ──────────────────────────────────────────
public record DeleteDocumentTemplateCommand(int Id) : IRequest<Result>;

public class DeleteDocumentTemplateCommandHandler : IRequestHandler<DeleteDocumentTemplateCommand, Result>
{
    private readonly IRepository<DocumentTemplate> _repo;
    private readonly IUnitOfWork _uow;
    public DeleteDocumentTemplateCommandHandler(IRepository<DocumentTemplate> repo, IUnitOfWork uow)
    { _repo = repo; _uow = uow; }

    public async Task<Result> Handle(DeleteDocumentTemplateCommand req, CancellationToken ct)
    {
        var t = await _repo.GetByIdAsync(req.Id, ct);
        if (t is null) return Result.Failure("قالب یافت نشد.");
        if (t.IsSystem) return Result.Failure("قالبِ سیستمی حذف نمی‌شود (می‌توانید غیرفعالش کنید).");
        _repo.Remove(t);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
