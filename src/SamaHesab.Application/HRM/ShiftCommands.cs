using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Entities.HRM;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.HRM;

/// <summary>ATTP-C1-1 — CRUDِ شیفتِ کاری (روی موجودیتِ Shift). زمان‌ها «HH:mm» (نرمال‌سازیِ رقمِ فارسی).</summary>
public record ShiftDto(int Id, string Name, string Start, string End, int BreakMinutes,
    bool IsNight, decimal StandardHours, bool IsActive, string? Notes);

public record GetShiftsQuery(bool ActiveOnly = false) : IRequest<List<ShiftDto>>;

public class GetShiftsQueryHandler : IRequestHandler<GetShiftsQuery, List<ShiftDto>>
{
    private readonly IRepository<Shift> _shifts;
    private readonly ICurrentUserService _user;
    public GetShiftsQueryHandler(IRepository<Shift> shifts, ICurrentUserService user) { _shifts = shifts; _user = user; }

    public async Task<List<ShiftDto>> Handle(GetShiftsQuery req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        return (await _shifts.FindAsync(s => s.CompanyId == companyId, ct))
            .Where(s => !req.ActiveOnly || s.IsActive)
            .OrderBy(s => s.Name)
            .Select(s => new ShiftDto(s.Id, s.Name, s.StartTime.ToString("HH:mm"), s.EndTime.ToString("HH:mm"),
                s.BreakMinutes, s.IsNight, s.StandardHours, s.IsActive, s.Notes))
            .ToList();
    }
}

public record SaveShiftCommand(int Id, string Name, string Start, string End,
    int BreakMinutes = 0, bool IsNight = false, decimal StandardHours = 7.33m, bool IsActive = true, string? Notes = null)
    : IRequest<Result<int>>;

public class SaveShiftCommandHandler : IRequestHandler<SaveShiftCommand, Result<int>>
{
    private readonly IRepository<Shift> _shifts;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;
    public SaveShiftCommandHandler(IRepository<Shift> shifts, IUnitOfWork uow, ICurrentUserService user)
    { _shifts = shifts; _uow = uow; _user = user; }

    public async Task<Result<int>> Handle(SaveShiftCommand req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Name)) return Result<int>.Failure("نامِ شیفت الزامی است.");
        if (!HrTime.TryParse(req.Start, out var start) || !HrTime.TryParse(req.End, out var end))
            return Result<int>.Failure("ساعتِ شروع/پایانِ نامعتبر (HH:mm).");
        var companyId = _user.CompanyId ?? 1;

        Shift shift;
        if (req.Id > 0)
        {
            shift = await _shifts.FindSingleAsync(s => s.Id == req.Id && s.CompanyId == companyId, ct)
                    ?? throw new InvalidOperationException("شیفت یافت نشد.");
            shift.Update(req.Name, start, end, req.BreakMinutes, req.IsNight, req.StandardHours, req.IsActive, req.Notes);
            _shifts.Update(shift);
        }
        else
        {
            shift = Shift.Create(companyId, req.Name, start, end, req.BreakMinutes, req.IsNight, req.StandardHours);
            if (!string.IsNullOrWhiteSpace(req.Notes) || !req.IsActive)
                shift.Update(req.Name, start, end, req.BreakMinutes, req.IsNight, req.StandardHours, req.IsActive, req.Notes);
            await _shifts.AddAsync(shift, ct);
        }
        await _uow.SaveChangesAsync(ct);
        return Result<int>.Success(shift.Id);
    }
}

public record DeleteShiftCommand(int Id) : IRequest<Result>;

public class DeleteShiftCommandHandler : IRequestHandler<DeleteShiftCommand, Result>
{
    private readonly IRepository<Shift> _shifts;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;
    public DeleteShiftCommandHandler(IRepository<Shift> shifts, IUnitOfWork uow, ICurrentUserService user)
    { _shifts = shifts; _uow = uow; _user = user; }

    public async Task<Result> Handle(DeleteShiftCommand req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        var shift = await _shifts.FindSingleAsync(s => s.Id == req.Id && s.CompanyId == companyId, ct);
        if (shift is null) return Result.Failure("شیفت یافت نشد.");
        shift.Deactivate();   // حذفِ نرم (شیفت ممکن است در رکوردهای گذشته استفاده شده باشد)
        _shifts.Update(shift);
        await _uow.SaveChangesAsync(ct);
        return Result.Success();
    }
}

/// <summary>کمکی: پارسِ «HH:mm» با نرمال‌سازیِ رقمِ فارسی/عربی.</summary>
internal static class HrTime
{
    public static bool TryParse(string? s, out TimeOnly time)
    {
        s = Normalize(s);
        return TimeOnly.TryParse(s, out time);
    }

    private static string Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var chars = input.Trim().ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            var c = chars[i];
            if (c >= '۰' && c <= '۹') chars[i] = (char)('0' + (c - '۰'));
            else if (c >= '٠' && c <= '٩') chars[i] = (char)('0' + (c - '٠'));
        }
        return new string(chars);
    }
}
