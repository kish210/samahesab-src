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

// ── سالِ مالیِ فعالِ شرکتِ جاری (U-ACCT-1.7) ────────────────────────────────────
// جایگزینِ هاردکدِ FiscalYearId=۱ در ViewModel/Controllerهایی که به مدیاتور دسترسی دارند؛
// منطقِ خالص در FiscalYearResolver (تست‌پذیر، بدونِ نیازِ IMediator).
public record GetActiveFiscalYearQuery() : IRequest<int>;

public class GetActiveFiscalYearQueryHandler : IRequestHandler<GetActiveFiscalYearQuery, int>
{
    private readonly IRepository<FiscalYear> _repo;
    private readonly ICurrentUserService _user;
    public GetActiveFiscalYearQueryHandler(IRepository<FiscalYear> repo, ICurrentUserService user)
    { _repo = repo; _user = user; }

    public Task<int> Handle(GetActiveFiscalYearQuery req, CancellationToken ct) =>
        SamaHesab.Application.Accounting.FiscalYearResolver.ResolveActiveIdAsync(_repo, _user.CompanyId ?? 1, ct);
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
                var companyId = _user.CompanyId ?? 1;
                // ایده‌مپوتنت: اگر سالِ مالیِ همان دوره (یا همان عنوان) از قبل هست (مثلاً seedِ نصب)،
                // به‌جای ساختِ تکراری همان را برگردان — تا ویزاردِ راه‌اندازی با «سال مالیِ تکراری» نشکند.
                var dup = (await _repo.FindAsync(
                    f => f.CompanyId == companyId && (f.StartDate == req.StartDate || f.Title == req.Title), ct))
                    .FirstOrDefault();
                if (dup != null)
                {
                    dup.Update(req.Title, req.StartDate, req.EndDate);   // هم‌سان با ورودیِ کاربر
                    _repo.Update(dup);
                    await _uow.SaveChangesAsync(ct);
                    return Result<int>.Success(dup.Id);
                }
                var fy = FiscalYear.Create(companyId, req.Title, req.StartDate, req.EndDate);
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
