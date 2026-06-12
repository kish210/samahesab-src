using FluentValidation;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Accounting.Dimensions;

// ── فهرست مراکز هزینه ──────────────────────────────────────────────────────────
public record GetCostCentersQuery(bool ActiveOnly = false) : IRequest<List<CostCenterDto>>;
public record CostCenterDto(int Id, string Code, string Name, int? ParentId, bool IsActive);

public class GetCostCentersQueryHandler : IRequestHandler<GetCostCentersQuery, List<CostCenterDto>>
{
    private readonly IRepository<CostCenter> _repo;
    public GetCostCentersQueryHandler(IRepository<CostCenter> repo) => _repo = repo;

    public async Task<List<CostCenterDto>> Handle(GetCostCentersQuery req, CancellationToken ct)
    {
        var list = req.ActiveOnly ? await _repo.FindAsync(c => c.IsActive, ct) : await _repo.GetAllAsync(ct);
        return list.OrderBy(c => c.Code)
            .Select(c => new CostCenterDto(c.Id, c.Code, c.Name, c.ParentId, c.IsActive))
            .ToList();
    }
}

// ── ذخیره (ایجاد/ویرایش) مرکز هزینه ────────────────────────────────────────────
public record SaveCostCenterCommand(int Id, string Code, string Name, int? ParentId) : IRequest<Result<int>>;

public class SaveCostCenterCommandValidator : AbstractValidator<SaveCostCenterCommand>
{
    public SaveCostCenterCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().WithMessage("کد مرکز هزینه الزامی است.");
        RuleFor(x => x.Name).NotEmpty().WithMessage("نام مرکز هزینه الزامی است.");
    }
}

public class SaveCostCenterCommandHandler : IRequestHandler<SaveCostCenterCommand, Result<int>>
{
    private readonly IRepository<CostCenter> _repo;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;
    public SaveCostCenterCommandHandler(IRepository<CostCenter> repo, IUnitOfWork uow, ICurrentUserService user)
    { _repo = repo; _uow = uow; _user = user; }

    public async Task<Result<int>> Handle(SaveCostCenterCommand req, CancellationToken ct)
    {
        try
        {
            if (req.Id == 0)
            {
                if (await _repo.AnyAsync(c => c.Code == req.Code, ct))
                    return Result<int>.Failure("کد مرکز هزینه تکراری است.");
                var cc = CostCenter.Create(_user.CompanyId ?? 1, req.Code, req.Name, req.ParentId);
                await _repo.AddAsync(cc, ct);
                await _uow.SaveChangesAsync(ct);
                return Result<int>.Success(cc.Id);
            }
            var existing = await _repo.GetByIdAsync(req.Id, ct);
            if (existing is null) return Result<int>.Failure("مرکز هزینه یافت نشد.");
            existing.Update(req.Name, req.ParentId);
            _repo.Update(existing);
            await _uow.SaveChangesAsync(ct);
            return Result<int>.Success(existing.Id);
        }
        catch (Exception ex) { return Result<int>.Failure(ex.GetBaseException().Message); }
    }
}

// ── فعال/غیرفعال ───────────────────────────────────────────────────────────────
public record SetCostCenterActiveCommand(int Id, bool Active) : IRequest<Result>;

public class SetCostCenterActiveCommandHandler : IRequestHandler<SetCostCenterActiveCommand, Result>
{
    private readonly IRepository<CostCenter> _repo;
    private readonly IUnitOfWork _uow;
    public SetCostCenterActiveCommandHandler(IRepository<CostCenter> repo, IUnitOfWork uow)
    { _repo = repo; _uow = uow; }

    public async Task<Result> Handle(SetCostCenterActiveCommand req, CancellationToken ct)
    {
        var cc = await _repo.GetByIdAsync(req.Id, ct);
        if (cc is null) return Result.Failure("مرکز هزینه یافت نشد.");
        if (req.Active) cc.Activate(); else cc.Deactivate();
        _repo.Update(cc);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
