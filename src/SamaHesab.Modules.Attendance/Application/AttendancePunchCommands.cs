using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Entities.HRM;
using SamaHesab.Domain.Interfaces.Repositories;
using SamaHesab.Modules.Attendance.Infrastructure;

namespace SamaHesab.Application.HRM;

// ════════════════════════════════════════════════════════════════════════════
// ATTP-C1-3 — دستگاهِ تردد (CRUD) + ثبتِ ضربهٔ خام + پردازشِ خام→روزانه (جفت‌سازی).
// ════════════════════════════════════════════════════════════════════════════

// ── دستگاه ──
public record DeviceDto(int Id, string Name, string? Code, string? Location, bool IsActive,
    string? IpAddress, int Port, string? CommKey);

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
            .Select(d => new DeviceDto(d.Id, d.Name, d.Code, d.Location, d.IsActive, d.IpAddress, d.Port, d.CommKey))
            .ToList();
    }
}

public record SaveDeviceCommand(int Id, string Name, string? Code = null, string? Location = null, bool IsActive = true,
    string? IpAddress = null, int Port = 4370, string? CommKey = null) : IRequest<Result<int>>;

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
            dev.Update(req.Name, req.Code, req.Location, req.IsActive, req.IpAddress, req.Port, req.CommKey);
            _devices.Update(dev);
        }
        else
        {
            dev = AttendanceDevice.Create(companyId, req.Name, req.Code, req.Location, req.IpAddress, req.Port, req.CommKey);
            await _devices.AddAsync(dev, ct);
        }
        await _uow.SaveChangesAsync(ct);
        return Result<int>.Success(dev.Id);
    }
}

// ── همگام‌سازیِ ترددِ خام از دستگاهِ زدکتکو (TCP/IP) ──
public record SyncDeviceAttendanceCommand(int DeviceId) : IRequest<Result<SyncDeviceAttendanceResult>>;

public record SyncDeviceAttendanceResult(int PunchesRead, int PunchesInserted, int DaysProcessed);

public class SyncDeviceAttendanceCommandHandler : IRequestHandler<SyncDeviceAttendanceCommand, Result<SyncDeviceAttendanceResult>>
{
    private readonly IRepository<AttendanceDevice> _devices;
    private readonly IRepository<Employee> _employees;
    private readonly IRepository<RawPunch> _punches;
    private readonly IMediator _mediator;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;

    public SyncDeviceAttendanceCommandHandler(IRepository<AttendanceDevice> devices, IRepository<Employee> employees,
        IRepository<RawPunch> punches, IMediator mediator, IUnitOfWork uow, ICurrentUserService user)
    { _devices = devices; _employees = employees; _punches = punches; _mediator = mediator; _uow = uow; _user = user; }

    public async Task<Result<SyncDeviceAttendanceResult>> Handle(SyncDeviceAttendanceCommand req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        var device = await _devices.FindSingleAsync(d => d.Id == req.DeviceId && d.CompanyId == companyId, ct)
            ?? throw new InvalidOperationException("دستگاه یافت نشد.");
        if (string.IsNullOrWhiteSpace(device.IpAddress))
            return Result<SyncDeviceAttendanceResult>.Failure("آدرسِ IPِ دستگاه تنظیم نشده است.");

        List<ZkPunch> punches;
        using (var client = new ZkTecoDeviceClient())
        {
            try
            {
                client.Connect(device.IpAddress!, device.Port, device.CommKey);
                punches = client.GetAttendanceLogs();
            }
            catch (Exception ex)
            {
                return Result<SyncDeviceAttendanceResult>.Failure($"اتصال به دستگاه ناموفق بود: {ex.Message}");
            }
        }

        var empByCode = (await _employees.FindAsync(e => e.CompanyId == companyId, ct))
            .GroupBy(e => e.Code).ToDictionary(g => g.Key, g => g.First());

        var pc = new System.Globalization.PersianCalendar();
        var affectedDates = new HashSet<string>();
        int inserted = 0;

        foreach (var p in punches)
        {
            if (!empByCode.TryGetValue(p.EmployeeCode, out var emp)) continue;
            var workDate = $"{pc.GetYear(p.Timestamp):D4}/{pc.GetMonth(p.Timestamp):D2}/{pc.GetDayOfMonth(p.Timestamp):D2}";
            var time = TimeOnly.FromDateTime(p.Timestamp);

            var exists = (await _punches.FindAsync(
                r => r.CompanyId == companyId && r.EmployeeId == emp.Id && r.WorkDate == workDate
                     && r.DeviceId == device.Id && r.PunchTime == time, ct)).Any();
            if (exists) continue;

            var raw = RawPunch.Create(companyId, emp.Id, workDate, time, device.Id);
            await _punches.AddAsync(raw, ct);
            inserted++;
            affectedDates.Add(workDate);
        }
        await _uow.SaveChangesAsync(ct);

        foreach (var date in affectedDates)
            await _mediator.Send(new ProcessRawPunchesCommand(date), ct);

        return Result<SyncDeviceAttendanceResult>.Success(
            new SyncDeviceAttendanceResult(punches.Count, inserted, affectedDates.Count));
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
