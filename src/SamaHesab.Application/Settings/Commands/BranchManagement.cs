using FluentValidation;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Entities.Settings;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Settings.Commands;

// ── فهرست شعب شرکت ─────────────────────────────────────────────────────────────
public record GetBranchesQuery() : IRequest<List<BranchDto>>;
public record BranchDto(int Id, string Code, string Name, string? Address, string? Phone,
    string? ManagerName, bool IsHQ, bool IsActive);

public class GetBranchesQueryHandler : IRequestHandler<GetBranchesQuery, List<BranchDto>>
{
    private readonly IRepository<Branch> _branches;
    private readonly ICurrentUserService _user;
    public GetBranchesQueryHandler(IRepository<Branch> branches, ICurrentUserService user)
    { _branches = branches; _user = user; }

    public async Task<List<BranchDto>> Handle(GetBranchesQuery req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        var list = await _branches.FindAsync(b => b.CompanyId == companyId, ct);
        return list.OrderByDescending(b => b.IsHQ).ThenBy(b => b.Code)
            .Select(b => new BranchDto(b.Id, b.Code, b.Name, b.Address, b.Phone, b.ManagerName, b.IsHQ, b.IsActive))
            .ToList();
    }
}

// ── ذخیرهٔ شعبه (ایجاد/ویرایش) ──────────────────────────────────────────────────
public record SaveBranchCommand(int Id, string Code, string Name, string? Address,
    string? Phone, string? ManagerName, bool IsHQ) : IRequest<Result<int>>;

public class SaveBranchCommandValidator : AbstractValidator<SaveBranchCommand>
{
    public SaveBranchCommandValidator()
    {
        RuleFor(x => x.Code).NotEmpty().WithMessage("کد شعبه الزامی است.");
        RuleFor(x => x.Name).NotEmpty().WithMessage("نام شعبه الزامی است.");
    }
}

public class SaveBranchCommandHandler : IRequestHandler<SaveBranchCommand, Result<int>>
{
    private readonly IRepository<Branch> _branches;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;
    public SaveBranchCommandHandler(IRepository<Branch> branches, IUnitOfWork uow, ICurrentUserService user)
    { _branches = branches; _uow = uow; _user = user; }

    public async Task<Result<int>> Handle(SaveBranchCommand req, CancellationToken ct)
    {
        try
        {
            var companyId = _user.CompanyId ?? 1;
            if (req.Id == 0)
            {
                if (await _branches.AnyAsync(b => b.CompanyId == companyId && b.Code == req.Code, ct))
                    return Result<int>.Failure("کد شعبه تکراری است.");
                var branch = Branch.Create(companyId, req.Code, req.Name, req.IsHQ);
                branch.Update(req.Name, req.Address, req.Phone, req.ManagerName);
                await _branches.AddAsync(branch, ct);
                await _uow.SaveChangesAsync(ct);
                return Result<int>.Success(branch.Id);
            }
            var existing = await _branches.GetByIdAsync(req.Id, ct);
            if (existing is null) return Result<int>.Failure("شعبه یافت نشد.");
            existing.Update(req.Name, req.Address, req.Phone, req.ManagerName);
            _branches.Update(existing);
            await _uow.SaveChangesAsync(ct);
            return Result<int>.Success(existing.Id);
        }
        catch (Exception ex) { return Result<int>.Failure(ex.GetBaseException().Message); }
    }
}

// ── فعال/غیرفعال‌سازی شعبه ──────────────────────────────────────────────────────
public record ToggleBranchCommand(int Id, bool Active) : IRequest<Result>;

public class ToggleBranchCommandHandler : IRequestHandler<ToggleBranchCommand, Result>
{
    private readonly IRepository<Branch> _branches;
    private readonly IUnitOfWork _uow;
    public ToggleBranchCommandHandler(IRepository<Branch> branches, IUnitOfWork uow)
    { _branches = branches; _uow = uow; }

    public async Task<Result> Handle(ToggleBranchCommand req, CancellationToken ct)
    {
        var b = await _branches.GetByIdAsync(req.Id, ct);
        if (b is null) return Result.Failure("شعبه یافت نشد.");
        if (b.IsHQ && !req.Active) return Result.Failure("شعبهٔ مرکزی را نمی‌توان غیرفعال کرد.");
        if (req.Active) b.Activate(); else b.Deactivate();
        _branches.Update(b);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
