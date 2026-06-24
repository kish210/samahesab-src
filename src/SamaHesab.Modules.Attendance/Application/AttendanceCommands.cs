using System.Globalization;
using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Entities.HRM;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.HRM;

// ════════════════════════════════════════════════════════════════════════════
// ATT-C1-2 — فرمان‌های ثبتِ تردد و مرخصی (مسیرِ نوشتنِ ماژولِ حضوروغیاب).
// ════════════════════════════════════════════════════════════════════════════

/// <summary>ثبت/ویرایشِ ترددِ یک کارمند در یک روز (idempotent بر EmployeeId+WorkDate).</summary>
public record UpsertAttendanceCommand(int EmployeeId, string WorkDate,
    TimeOnly? CheckIn = null, TimeOnly? CheckOut = null,
    string Status = "حاضر", string? LeaveType = null, string? Notes = null)
    : IRequest<Result<int>>;

public class UpsertAttendanceCommandHandler : IRequestHandler<UpsertAttendanceCommand, Result<int>>
{
    private readonly IRepository<Employee> _employees;
    private readonly IRepository<AttendanceRecord> _records;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;

    public UpsertAttendanceCommandHandler(IRepository<Employee> employees, IRepository<AttendanceRecord> records,
        IUnitOfWork uow, ICurrentUserService user)
    { _employees = employees; _records = records; _uow = uow; _user = user; }

    public async Task<Result<int>> Handle(UpsertAttendanceCommand req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.WorkDate)) return Result<int>.Failure("تاریخ الزامی است.");
        var companyId = _user.CompanyId ?? 1;
        var emp = await _employees.FindSingleAsync(e => e.Id == req.EmployeeId && e.CompanyId == companyId, ct);
        if (emp is null) return Result<int>.Failure("کارمند یافت نشد.");

        var rec = await _records.FindSingleAsync(a => a.EmployeeId == req.EmployeeId && a.WorkDate == req.WorkDate, ct);
        var isNew = rec is null;
        rec ??= AttendanceRecord.Create(req.EmployeeId, req.WorkDate, req.Status);

        AttendanceCommandHelpers.ApplyStatus(rec, req.Status, req.LeaveType, req.Notes, req.CheckIn, req.CheckOut);

        if (isNew) await _records.AddAsync(rec, ct);
        await _uow.SaveChangesAsync(ct);
        return Result<int>.Success(rec.Id);
    }
}

/// <summary>علامت‌گذاریِ دسته‌ایِ یک وضعیت (غیبت/مرخصی/حاضر) برای چند کارمند در یک روز.</summary>
public record MarkBatchAttendanceCommand(IReadOnlyList<int> EmployeeIds, string WorkDate,
    string Status, string? LeaveType = null, string? Notes = null)
    : IRequest<Result<int>>;

public class MarkBatchAttendanceCommandHandler : IRequestHandler<MarkBatchAttendanceCommand, Result<int>>
{
    private readonly IRepository<Employee> _employees;
    private readonly IRepository<AttendanceRecord> _records;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;

    public MarkBatchAttendanceCommandHandler(IRepository<Employee> employees, IRepository<AttendanceRecord> records,
        IUnitOfWork uow, ICurrentUserService user)
    { _employees = employees; _records = records; _uow = uow; _user = user; }

    public async Task<Result<int>> Handle(MarkBatchAttendanceCommand req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.WorkDate)) return Result<int>.Failure("تاریخ الزامی است.");
        if (req.EmployeeIds is null || req.EmployeeIds.Count == 0) return Result<int>.Failure("کارمندی انتخاب نشده.");
        var companyId = _user.CompanyId ?? 1;

        var ids = req.EmployeeIds.ToHashSet();
        var emps = (await _employees.FindAsync(e => e.CompanyId == companyId, ct))
            .Where(e => ids.Contains(e.Id)).Select(e => e.Id).ToHashSet();
        if (emps.Count == 0) return Result<int>.Failure("کارمندِ معتبری یافت نشد.");

        // فیلترِ کارمندِ شرکت در خودِ کوئری (AttendanceRecord بدونِ CompanyId است).
        var existing = (await _records.FindAsync(a => a.WorkDate == req.WorkDate && emps.Contains(a.EmployeeId), ct))
            .ToDictionary(a => a.EmployeeId, a => a);

        int n = 0;
        foreach (var empId in emps)
        {
            if (!existing.TryGetValue(empId, out var rec))
            {
                rec = AttendanceRecord.Create(empId, req.WorkDate, req.Status);
                AttendanceCommandHelpers.ApplyStatus(rec, req.Status, req.LeaveType, req.Notes, null, null);
                await _records.AddAsync(rec, ct);
            }
            else
            {
                AttendanceCommandHelpers.ApplyStatus(rec, req.Status, req.LeaveType, req.Notes, null, null);
            }
            n++;
        }
        await _uow.SaveChangesAsync(ct);
        return Result<int>.Success(n);
    }
}

/// <summary>درخواستِ مرخصی (در وضعیتِ «درخواست»). مرخصیِ استحقاقی برابرِ ماندهٔ سال اعتبارسنجی می‌شود.</summary>
public record RequestLeaveCommand(int EmployeeId, string LeaveType, string StartDate, string EndDate,
    decimal Days, decimal Hours = 0, string? Reason = null) : IRequest<Result<int>>;

public class RequestLeaveCommandHandler : IRequestHandler<RequestLeaveCommand, Result<int>>
{
    private readonly IRepository<Employee> _employees;
    private readonly IRepository<LeaveRequest> _leaves;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;

    public RequestLeaveCommandHandler(IRepository<Employee> employees, IRepository<LeaveRequest> leaves,
        IUnitOfWork uow, ICurrentUserService user)
    { _employees = employees; _leaves = leaves; _uow = uow; _user = user; }

    public async Task<Result<int>> Handle(RequestLeaveCommand req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        var emp = await _employees.FindSingleAsync(e => e.Id == req.EmployeeId && e.CompanyId == companyId, ct);
        if (emp is null) return Result<int>.Failure("کارمند یافت نشد.");

        LeaveRequest leave;
        try
        {
            leave = LeaveRequest.Create(companyId, req.EmployeeId, req.LeaveType,
                req.StartDate, req.EndDate, req.Days, req.Hours, req.Reason);
        }
        catch (ArgumentException ex) { return Result<int>.Failure(ex.Message); }

        // مرخصیِ استحقاقی نباید از ماندهٔ سال بیشتر باشد (موتورِ ATT-C2-2).
        if (leave.LeaveType == LeaveRequest.TypeAnnual)
        {
            var (year, month) = AttendanceCommandHelpers.YearMonth(req.StartDate);
            var rules = new LeaveRules();
            var approved = (await _leaves.FindAsync(
                    l => l.EmployeeId == req.EmployeeId && l.Status == LeaveRequest.StatusApproved
                         && l.LeaveType == LeaveRequest.TypeAnnual, ct))
                .Where(l => AttendanceCommandHelpers.YearMonth(l.StartDate).Year == year)
                .ToList();
            var used = new LeaveUsage(approved.Sum(l => l.Days), approved.Sum(l => l.Hours));
            var balance = LeaveBalanceCalculator.Compute(month, used, 0, rules);
            var requestDays = leave.Days + (rules.WorkHoursPerDay > 0 ? leave.Hours / rules.WorkHoursPerDay : 0);
            if (!LeaveBalanceCalculator.CanRequest(balance, requestDays))
                return Result<int>.Failure($"ماندهٔ مرخصیِ استحقاقی کافی نیست (مانده: {balance.RemainingDays} روز).");
        }

        await _leaves.AddAsync(leave, ct);
        await _uow.SaveChangesAsync(ct);
        return Result<int>.Success(leave.Id);
    }
}

/// <summary>تأیید/ردِ یک درخواستِ مرخصی.</summary>
public record DecideLeaveCommand(int LeaveRequestId, bool Approve, string DecisionDate, string? Note = null)
    : IRequest<Result<int>>;

public class DecideLeaveCommandHandler : IRequestHandler<DecideLeaveCommand, Result<int>>
{
    private readonly IRepository<LeaveRequest> _leaves;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;

    public DecideLeaveCommandHandler(IRepository<LeaveRequest> leaves, IUnitOfWork uow, ICurrentUserService user)
    { _leaves = leaves; _uow = uow; _user = user; }

    public async Task<Result<int>> Handle(DecideLeaveCommand req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        var leave = await _leaves.FindSingleAsync(l => l.Id == req.LeaveRequestId && l.CompanyId == companyId, ct);
        if (leave is null) return Result<int>.Failure("درخواستِ مرخصی یافت نشد.");

        try
        {
            if (req.Approve) leave.Approve(_user.UserId ?? 0, req.DecisionDate, req.Note);
            else leave.Reject(_user.UserId ?? 0, req.DecisionDate, req.Note);
        }
        catch (InvalidOperationException ex) { return Result<int>.Failure(ex.Message); }

        _leaves.Update(leave);
        await _uow.SaveChangesAsync(ct);
        return Result<int>.Success(leave.Id);
    }
}

internal static class AttendanceCommandHelpers
{
    public static void ApplyStatus(AttendanceRecord rec, string status, string? leaveType, string? notes,
        TimeOnly? checkIn, TimeOnly? checkOut)
    {
        switch (status)
        {
            case "مرخصی": rec.SetLeave(leaveType ?? "استحقاقی", notes); break;
            case "غایب": rec.SetAbsent(notes); break;
            default:
                if (checkIn.HasValue) rec.SetCheckIn(checkIn.Value);
                if (checkOut.HasValue) rec.SetCheckOut(checkOut.Value);
                break;
        }
    }

    /// <summary>استخراجِ سال و ماه از تاریخِ شمسیِ «YYYY/MM/DD».</summary>
    public static (int Year, int Month) YearMonth(string shamsiDate)
    {
        if (string.IsNullOrWhiteSpace(shamsiDate)) return (0, 0);
        var parts = shamsiDate.Replace('-', '/').Split('/');
        int.TryParse(parts.ElementAtOrDefault(0), NumberStyles.Integer, CultureInfo.InvariantCulture, out var y);
        var year = y;
        int month = 0;
        if (parts.Length > 1) int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out month);
        return (year, month);
    }
}
