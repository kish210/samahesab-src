using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Entities.HRM;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.HRM;

/// <summary>ATTP-C1-2 — CRUDِ تقویمِ کاری/تعطیلات (روی موجودیتِ Holiday).</summary>
public record HolidayDto(int Id, string Date, string Title, bool IsOfficial);

public record GetHolidaysQuery(string? Year = null) : IRequest<List<HolidayDto>>;

public class GetHolidaysQueryHandler : IRequestHandler<GetHolidaysQuery, List<HolidayDto>>
{
    private readonly IRepository<Holiday> _holidays;
    private readonly ICurrentUserService _user;
    public GetHolidaysQueryHandler(IRepository<Holiday> holidays, ICurrentUserService user) { _holidays = holidays; _user = user; }

    public async Task<List<HolidayDto>> Handle(GetHolidaysQuery req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        var prefix = string.IsNullOrWhiteSpace(req.Year) ? null : req.Year + "/";
        return (await _holidays.FindAsync(h => h.CompanyId == companyId, ct))
            .Where(h => prefix == null || (h.Date != null && h.Date.StartsWith(prefix)))
            .OrderBy(h => h.Date)
            .Select(h => new HolidayDto(h.Id, h.Date, h.Title, h.IsOfficial))
            .ToList();
    }
}

public record SaveHolidayCommand(int Id, string Date, string Title, bool IsOfficial = true) : IRequest<Result<int>>;

public class SaveHolidayCommandHandler : IRequestHandler<SaveHolidayCommand, Result<int>>
{
    private readonly IRepository<Holiday> _holidays;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;
    public SaveHolidayCommandHandler(IRepository<Holiday> holidays, IUnitOfWork uow, ICurrentUserService user)
    { _holidays = holidays; _uow = uow; _user = user; }

    public async Task<Result<int>> Handle(SaveHolidayCommand req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Date)) return Result<int>.Failure("تاریخِ تعطیلی الزامی است.");
        var companyId = _user.CompanyId ?? 1;

        Holiday h;
        if (req.Id > 0)
        {
            h = await _holidays.FindSingleAsync(x => x.Id == req.Id && x.CompanyId == companyId, ct)
                ?? throw new InvalidOperationException("تعطیلی یافت نشد.");
            h.Update(req.Title, req.IsOfficial);
            _holidays.Update(h);
        }
        else
        {
            // یکتایی بر شرکت+تاریخ (تکراری نشود).
            if (await _holidays.AnyAsync(x => x.CompanyId == companyId && x.Date == req.Date, ct))
                return Result<int>.Failure("برای این تاریخ تعطیلی ثبت شده است.");
            h = Holiday.Create(companyId, req.Date, req.Title, req.IsOfficial);
            await _holidays.AddAsync(h, ct);
        }
        await _uow.SaveChangesAsync(ct);
        return Result<int>.Success(h.Id);
    }
}

public record DeleteHolidayCommand(int Id) : IRequest<Result>;

public class DeleteHolidayCommandHandler : IRequestHandler<DeleteHolidayCommand, Result>
{
    private readonly IRepository<Holiday> _holidays;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;
    public DeleteHolidayCommandHandler(IRepository<Holiday> holidays, IUnitOfWork uow, ICurrentUserService user)
    { _holidays = holidays; _uow = uow; _user = user; }

    public async Task<Result> Handle(DeleteHolidayCommand req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        var h = await _holidays.FindSingleAsync(x => x.Id == req.Id && x.CompanyId == companyId, ct);
        if (h is null) return Result.Failure("تعطیلی یافت نشد.");
        _holidays.Remove(h);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}
