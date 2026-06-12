using FluentValidation;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Accounting.Dimensions;

// ── فهرست پروژه‌ها ─────────────────────────────────────────────────────────────
public record GetProjectsQuery(bool ActiveOnly = false) : IRequest<List<ProjectDto>>;
public record ProjectDto(int Id, string Code, string Name, string? StartDate, string? EndDate,
    decimal Budget, bool IsClosed, bool IsActive);

public class GetProjectsQueryHandler : IRequestHandler<GetProjectsQuery, List<ProjectDto>>
{
    private readonly IRepository<Project> _repo;
    public GetProjectsQueryHandler(IRepository<Project> repo) => _repo = repo;

    public async Task<List<ProjectDto>> Handle(GetProjectsQuery req, CancellationToken ct)
    {
        var list = req.ActiveOnly ? await _repo.FindAsync(p => p.IsActive, ct) : await _repo.GetAllAsync(ct);
        return list.OrderBy(p => p.Code)
            .Select(p => new ProjectDto(p.Id, p.Code, p.Name, p.StartDate, p.EndDate, p.Budget, p.IsClosed, p.IsActive))
            .ToList();
    }
}

// ── ذخیره (ایجاد/ویرایش) پروژه ─────────────────────────────────────────────────
public record SaveProjectCommand(int Id, string Code, string Name, string? StartDate, string? EndDate, decimal Budget)
    : IRequest<Result<int>>;

public class SaveProjectCommandValidator : AbstractValidator<SaveProjectCommand>
{
    public SaveProjectCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().WithMessage("کد پروژه الزامی است.");
        RuleFor(x => x.Name).NotEmpty().WithMessage("نام پروژه الزامی است.");
    }
}

public class SaveProjectCommandHandler : IRequestHandler<SaveProjectCommand, Result<int>>
{
    private readonly IRepository<Project> _repo;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;
    public SaveProjectCommandHandler(IRepository<Project> repo, IUnitOfWork uow, ICurrentUserService user)
    { _repo = repo; _uow = uow; _user = user; }

    public async Task<Result<int>> Handle(SaveProjectCommand req, CancellationToken ct)
    {
        try
        {
            if (req.Id == 0)
            {
                if (await _repo.AnyAsync(p => p.Code == req.Code, ct))
                    return Result<int>.Failure("کد پروژه تکراری است.");
                var p = Project.Create(_user.CompanyId ?? 1, req.Code, req.Name, req.StartDate, req.EndDate, req.Budget);
                await _repo.AddAsync(p, ct);
                await _uow.SaveChangesAsync(ct);
                return Result<int>.Success(p.Id);
            }
            var existing = await _repo.GetByIdAsync(req.Id, ct);
            if (existing is null) return Result<int>.Failure("پروژه یافت نشد.");
            existing.Update(req.Name, req.StartDate, req.EndDate, req.Budget);
            _repo.Update(existing);
            await _uow.SaveChangesAsync(ct);
            return Result<int>.Success(existing.Id);
        }
        catch (Exception ex) { return Result<int>.Failure(ex.GetBaseException().Message); }
    }
}

// ── باز/بسته کردن پروژه ────────────────────────────────────────────────────────
public record SetProjectClosedCommand(int Id, bool Closed) : IRequest<Result>;

public class SetProjectClosedCommandHandler : IRequestHandler<SetProjectClosedCommand, Result>
{
    private readonly IRepository<Project> _repo;
    private readonly IUnitOfWork _uow;
    public SetProjectClosedCommandHandler(IRepository<Project> repo, IUnitOfWork uow)
    { _repo = repo; _uow = uow; }

    public async Task<Result> Handle(SetProjectClosedCommand req, CancellationToken ct)
    {
        var p = await _repo.GetByIdAsync(req.Id, ct);
        if (p is null) return Result.Failure("پروژه یافت نشد.");
        if (req.Closed) p.Close(); else p.Reopen();
        _repo.Update(p);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
