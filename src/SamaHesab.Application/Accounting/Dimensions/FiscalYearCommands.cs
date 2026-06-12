using FluentValidation;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Accounting.Dimensions;

// ── فهرست سال‌های مالی ─────────────────────────────────────────────────────────
public record GetFiscalYearsQuery() : IRequest<List<FiscalYearDto>>;
public record FiscalYearDto(int Id, string Title, string StartDate, string EndDate, bool IsClosed, bool IsActive);

public class GetFiscalYearsQueryHandler : IRequestHandler<GetFiscalYearsQuery, List<FiscalYearDto>>
{
    private readonly IRepository<FiscalYear> _repo;
    public GetFiscalYearsQueryHandler(IRepository<FiscalYear> repo) => _repo = repo;

    public async Task<List<FiscalYearDto>> Handle(GetFiscalYearsQuery req, CancellationToken ct)
    {
        var list = await _repo.GetAllAsync(ct);
        return list.OrderByDescending(f => f.StartDate)
            .Select(f => new FiscalYearDto(f.Id, f.Title, f.StartDate, f.EndDate, f.IsClosed, f.IsActive))
            .ToList();
    }
}

// ── ذخیره (ایجاد/ویرایش) سال مالی ──────────────────────────────────────────────
public record SaveFiscalYearCommand(int Id, string Title, string StartDate, string EndDate)
    : IRequest<Result<int>>;

public class SaveFiscalYearCommandValidator : AbstractValidator<SaveFiscalYearCommand>
{
    public SaveFiscalYearCommandValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage("عنوان سال مالی الزامی است.");
        RuleFor(x => x.StartDate).NotEmpty();
        RuleFor(x => x.EndDate).NotEmpty();
    }
}

public class SaveFiscalYearCommandHandler : IRequestHandler<SaveFiscalYearCommand, Result<int>>
{
    private readonly IRepository<FiscalYear> _repo;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;
    public SaveFiscalYearCommandHandler(IRepository<FiscalYear> repo, IUnitOfWork uow, ICurrentUserService user)
    { _repo = repo; _uow = uow; _user = user; }

    public async Task<Result<int>> Handle(SaveFiscalYearCommand req, CancellationToken ct)
    {
        try
        {
            if (req.Id == 0)
            {
                var fy = FiscalYear.Create(_user.CompanyId ?? 1, req.Title, req.StartDate, req.EndDate);
                await _repo.AddAsync(fy, ct);
                await _uow.SaveChangesAsync(ct);
                return Result<int>.Success(fy.Id);
            }
            var existing = await _repo.GetByIdAsync(req.Id, ct);
            if (existing is null) return Result<int>.Failure("سال مالی یافت نشد.");
            existing.Update(req.Title, req.StartDate, req.EndDate);
            _repo.Update(existing);
            await _uow.SaveChangesAsync(ct);
            return Result<int>.Success(existing.Id);
        }
        catch (Exception ex) { return Result<int>.Failure(ex.GetBaseException().Message); }
    }
}

// ── باز/بسته کردن سال مالی (قفل دوره) ──────────────────────────────────────────
public record SetFiscalYearClosedCommand(int Id, bool Closed) : IRequest<Result>;

public class SetFiscalYearClosedCommandHandler : IRequestHandler<SetFiscalYearClosedCommand, Result>
{
    private readonly IRepository<FiscalYear> _repo;
    private readonly IUnitOfWork _uow;
    public SetFiscalYearClosedCommandHandler(IRepository<FiscalYear> repo, IUnitOfWork uow)
    { _repo = repo; _uow = uow; }

    public async Task<Result> Handle(SetFiscalYearClosedCommand req, CancellationToken ct)
    {
        var fy = await _repo.GetByIdAsync(req.Id, ct);
        if (fy is null) return Result.Failure("سال مالی یافت نشد.");
        if (req.Closed) fy.Close(); else fy.Reopen();
        _repo.Update(fy);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
