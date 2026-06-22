using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Entities.HRM;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.HRM;

// ════════════════════════════════════════════════════════════════════════════
// ATTP-C1-3 — دستگاهِ تردد (CRUD) + ثبتِ ضربهٔ خام + پردازشِ خام→روزانه (جفت‌سازی).
// ════════════════════════════════════════════════════════════════════════════

// ── دستگاه ──
public record DeviceDto(int Id, string Name, string? Code, string? Location, bool IsActive);

public record GetDevicesQuery(bool ActiveOnly = false) : IRequest<List<DeviceDto>>;

public class GetDevicesQueryHandler : IRequestHandler<GetDevicesQuery, List<DeviceDto>>
{
    private readonly IRepository<AttendanceDevice> _devices;
    private readonly ICurrentUserService _user;
    public GetDevicesQueryHandler(IRepository<AttendanceDevice> devices, ICurrentUserService user) { _devices = devices; _user = user; }

    public async Task<List<DeviceDto>> Handle(GetDevicesQuery req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        return (await _devices.FindAsync(d => d.CompanyId == companyId, ct))
            .Where(d => !req.ActiveOnly || d.IsActive)
            .OrderBy(d => d.Name)
            .Select(d => new DeviceDto(d.Id, d.Name, d.Code, d.Location, d.IsActive))
            .ToList();
    }
}

public record SaveDeviceCommand(int Id, string Name, string? Code = null, string? Location = null, bool IsActive = true)
    : IRequest<Result<int>>;

public class SaveDeviceCommandHandler : IRequestHandler<SaveDeviceCommand, Result<int>>
{
    private readonly IRepository<AttendanceDevice> _devices;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;
    public SaveDeviceCommandHandler(IRepository<AttendanceDevice> devices, IUnitOfWork uow, ICurrentUserService user)
    { _devices = devices; _uow = uow; _user = user; }

    public async Task<Result<int>> Handle(SaveDeviceCommand req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Name)) return Result<int>.Failure("نامِ دستگاه الزامی است.");
        var companyId = _user.CompanyId ?? 1;
        AttendanceDevice dev;
        if (req.Id > 0)
        {
            dev = await _devices.FindSingleAsync(d => d.Id == req.Id && d.CompanyId == companyId, ct)
                  ?? throw new InvalidOperationException("دستگاه یافت نشد.");
            dev.Update(req.Name, req.Code, req.Location, req.IsActive);
            _devices.Update(dev);
        }
        else
        {
            dev = AttendanceDevice.Create(companyId, req.Name, req.Code, req.Location);
            await _devices.AddAsync(dev, ct);
        }
        await _uow.SaveChangesAsync(ct);
        return Result<int>.Success(dev.Id);
    }
}

// ── ثبتِ ضربهٔ خام ──
public record RecordPunchCommand(int EmployeeId, string WorkDate, string Time, int? DeviceId = null) : IRequest<Result<int>>;

public class RecordPunchCommandHandler : IRequestHandler<RecordPunchCommand, Result<int>>
{
    private readonly IRepository<RawPunch> _punches;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;
    public RecordPunchCommandHandler(IRepository<RawPunch> punches, IUnitOfWork uow, ICurrentUserService user)
    { _punches = punches; _uow = uow; _user = user; }

    public async Task<Result<int>> Handle(RecordPunchCommand req, CancellationToken ct)
    {
        if (req.EmployeeId <= 0) return Result<int>.Failure("کارمند الزامی است.");
        if (!HrTime.TryParse(req.Time, out var time)) return Result<int>.Failure("ساعتِ نامعتبر (HH:mm).");
        if (string.IsNullOrWhiteSpace(req.WorkDate)) return Result<int>.Failure("تاریخ الزامی است.");
        var companyId = _user.CompanyId ?? 1;
        var p = RawPunch.Create(companyId, req.EmployeeId, req.WorkDate, time, req.DeviceId);
        await _punches.AddAsync(p, ct);
        await _uow.SaveChangesAsync(ct);
        return Result<int>.Success(p.Id);
    }
}

// ── پردازشِ خام→روزانه (جفت‌سازی: اولین=ورود، آخرین=خروج) ──
public record ProcessRawPunchesCommand(string WorkDate) : IRequest<Result<int>>;

public class ProcessRawPunchesCommandHandler : IRequestHandler<ProcessRawPunchesCommand, Result<int>>
{
    private readonly IRepository<RawPunch> _punches;
    private readonly IRepository<AttendanceRecord> _records;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;
    public ProcessRawPunchesCommandHandler(IRepository<RawPunch> punches, IRepository<AttendanceRecord> records,
        IUnitOfWork uow, ICurrentUserService user)
    { _punches = punches; _records = records; _uow = uow; _user = user; }

    public async Task<Result<int>> Handle(ProcessRawPunchesCommand req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.WorkDate)) return Result<int>.Failure("تاریخ الزامی است.");
        var companyId = _user.CompanyId ?? 1;

        var punches = (await _punches.FindAsync(
            p => p.CompanyId == companyId && p.WorkDate == req.WorkDate && !p.Processed, ct)).ToList();
        if (punches.Count == 0) return Result<int>.Success(0);

        int employeesProcessed = 0;
        foreach (var g in punches.GroupBy(p => p.EmployeeId))
        {
            var ordered = g.OrderBy(p => p.PunchTime).ToList();
            var checkIn = ordered.First().PunchTime;
            var checkOut = ordered.Count > 1 ? ordered.Last().PunchTime : (TimeOnly?)null;

            var rec = await _records.FindSingleAsync(
                a => a.EmployeeId == g.Key && a.WorkDate == req.WorkDate, ct);
            var isNew = rec is null;
            rec ??= AttendanceRecord.Create(g.Key, req.WorkDate);
            rec.SetCheckIn(checkIn);
            if (checkOut is TimeOnly co) rec.SetCheckOut(co);
            if (isNew) await _records.AddAsync(rec, ct);

            foreach (var p in ordered) { p.MarkProcessed(); _punches.Update(p); }
            employeesProcessed++;
        }

        await _uow.SaveChangesAsync(ct);
        return Result<int>.Success(employeesProcessed);
    }
}
