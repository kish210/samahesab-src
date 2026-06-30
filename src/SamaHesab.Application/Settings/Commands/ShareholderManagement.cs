using FluentValidation;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Entities.Settings;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Settings.Commands;

// ── فهرستِ سهامداران ────────────────────────────────────────────────────────────
public record GetShareholdersQuery() : IRequest<List<ShareholderDto>>;
public record ShareholderDto(int Id, string FullName, string? NationalCode, decimal SharePercent,
    decimal CapitalAmount, string? Phone, string? Notes, bool IsActive);

public class GetShareholdersQueryHandler : IRequestHandler<GetShareholdersQuery, List<ShareholderDto>>
{
    private readonly IRepository<Shareholder> _repo;
    private readonly ICurrentUserService _user;
    public GetShareholdersQueryHandler(IRepository<Shareholder> repo, ICurrentUserService user)
    { _repo = repo; _user = user; }

    public async Task<List<ShareholderDto>> Handle(GetShareholdersQuery req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        var list = await _repo.FindAsync(s => s.CompanyId == companyId, ct);
        return list.OrderByDescending(s => s.SharePercent).ThenBy(s => s.FullName)
            .Select(s => new ShareholderDto(s.Id, s.FullName, s.NationalCode, s.SharePercent,
                s.CapitalAmount, s.Phone, s.Notes, s.IsActive))
            .ToList();
    }
}

// ── ذخیرهٔ سهامدار (ایجاد/ویرایش) ────────────────────────────────────────────────
public record SaveShareholderCommand(int Id, string FullName, string? NationalCode, decimal SharePercent,
    decimal CapitalAmount, string? Phone, string? Notes) : IRequest<Result<int>>;

public class SaveShareholderCommandValidator : AbstractValidator<SaveShareholderCommand>
{
    public SaveShareholderCommandValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().WithMessage("نامِ سهامدار الزامی است.");
        RuleFor(x => x.SharePercent).InclusiveBetween(0, 100).WithMessage("درصدِ سهم باید بینِ ۰ تا ۱۰۰ باشد.");
        RuleFor(x => x.CapitalAmount).GreaterThanOrEqualTo(0).WithMessage("آورده نمی‌تواند منفی باشد.");
    }
}

public class SaveShareholderCommandHandler : IRequestHandler<SaveShareholderCommand, Result<int>>
{
    private readonly IRepository<Shareholder> _repo;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;
    public SaveShareholderCommandHandler(IRepository<Shareholder> repo, IUnitOfWork uow, ICurrentUserService user)
    { _repo = repo; _uow = uow; _user = user; }

    public async Task<Result<int>> Handle(SaveShareholderCommand req, CancellationToken ct)
    {
        try
        {
            var companyId = _user.CompanyId ?? 1;
            if (req.Id == 0)
            {
                var s = Shareholder.Create(companyId, req.FullName, req.SharePercent, req.CapitalAmount);
                s.Update(req.FullName, req.NationalCode, req.SharePercent, req.CapitalAmount, req.Phone, req.Notes);
                await _repo.AddAsync(s, ct);
                await _uow.SaveChangesAsync(ct);
                return Result<int>.Success(s.Id);
            }
            var existing = await _repo.GetByIdAsync(req.Id, ct);
            if (existing is null) return Result<int>.Failure("سهامدار یافت نشد.");
            existing.Update(req.FullName, req.NationalCode, req.SharePercent, req.CapitalAmount, req.Phone, req.Notes);
            _repo.Update(existing);
            await _uow.SaveChangesAsync(ct);
            return Result<int>.Success(existing.Id);
        }
        catch (Exception ex) { return Result<int>.Failure(ex.GetBaseException().Message); }
    }
}

// ── حذفِ سهامدار ─────────────────────────────────────────────────────────────────
public record DeleteShareholderCommand(int Id) : IRequest<Result>;

public class DeleteShareholderCommandHandler : IRequestHandler<DeleteShareholderCommand, Result>
{
    private readonly IRepository<Shareholder> _repo;
    private readonly IUnitOfWork _uow;
    public DeleteShareholderCommandHandler(IRepository<Shareholder> repo, IUnitOfWork uow)
    { _repo = repo; _uow = uow; }

    public async Task<Result> Handle(DeleteShareholderCommand req, CancellationToken ct)
    {
        var s = await _repo.GetByIdAsync(req.Id, ct);
        if (s is null) return Result.Failure("سهامدار یافت نشد.");
        _repo.Remove(s);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
