using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Entities.HRM;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.HRM;

/// <summary>
/// PAY-C1-3 — محاسبهٔ دسته‌ایِ حقوقِ یک ماه برای همهٔ کارکنانِ فعال و ذخیرهٔ فیش‌ها.
/// نرخ‌ها/مبالغِ پایه از تنظیماتِ سالِ حقوق (PAY-C1-5) خوانده می‌شود؛ موتورِ محاسبه `PayrollCalculator.ComputeFull`.
/// idempotent: اگر فیشِ همان کارمند/ماه باشد، رد می‌شود (مگر Overwrite=true که جایگزین می‌کند).
/// </summary>
public record RunMonthlyPayrollCommand(string Year, byte Month, bool Overwrite = false, bool UseAttendance = false)
    : IRequest<Result<RunPayrollResult>>;

public record RunPayrollResult(
    int Created, int Skipped,
    decimal TotalGross, decimal TotalNet,
    decimal TotalEmployeeInsurance, decimal TotalEmployerInsurance, decimal TotalTax);

public class RunMonthlyPayrollCommandHandler : IRequestHandler<RunMonthlyPayrollCommand, Result<RunPayrollResult>>
{
    private readonly IRepository<Employee> _employees;
    private readonly IRepository<SalarySlip> _slips;
    private readonly IRepository<PayrollSetting> _settings;
    private readonly IRepository<AttendanceRecord> _records;
    private readonly IRepository<Holiday> _holidays;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;

    public RunMonthlyPayrollCommandHandler(IRepository<Employee> employees, IRepository<SalarySlip> slips,
        IRepository<PayrollSetting> settings, IRepository<AttendanceRecord> records, IRepository<Holiday> holidays,
        IUnitOfWork uow, ICurrentUserService user)
    { _employees = employees; _slips = slips; _settings = settings; _records = records; _holidays = holidays; _uow = uow; _user = user; }

    public async Task<Result<RunPayrollResult>> Handle(RunMonthlyPayrollCommand req, CancellationToken ct)
    {
        if (req.Month < 1 || req.Month > 12) return Result<RunPayrollResult>.Failure("ماهِ نامعتبر.");
        var companyId = _user.CompanyId ?? 1;

        var emps = await _employees.FindAsync(e => e.CompanyId == companyId && e.IsActive, ct);
        if (emps.Count == 0) return Result<RunPayrollResult>.Failure("کارمندِ فعالی نیست.");

        // تنظیماتِ سال → نرخ‌ها و مبالغِ پایه (یا پیش‌فرض).
        var setRow = await _settings.FindSingleAsync(s => s.CompanyId == companyId && s.Year == req.Year, ct);
        var set = setRow is null ? PayrollSettingsDto.Default(req.Year) : PayrollSettingsDto.From(setRow);
        var rates = set.ToRates();

        var empIds = emps.Select(e => e.Id).ToHashSet();
        var existing = (await _slips.FindAsync(
                s => s.PeriodYear == req.Year && s.PeriodMonth == req.Month, ct))
            .Where(s => empIds.Contains(s.EmployeeId))
            .ToDictionary(s => s.EmployeeId, s => s);

        // پلِ تردد→حقوق (ATT-C1-3): اگر خواسته شد، رکوردهای ماه را به ساعت/غیبت تبدیل کن.
        Dictionary<int, MonthlyAttendance> attByEmp = new();
        if (req.UseAttendance)
        {
            var prefix = $"{req.Year}/{req.Month:D2}/";
            var holidaySet = (await _holidays.FindAsync(h => h.CompanyId == companyId, ct))
                .Select(h => h.Date).ToHashSet();
            var monthRecs = (await _records.FindAsync(a => a.WorkDate != null && a.WorkDate.StartsWith(prefix), ct))
                .Where(a => empIds.Contains(a.EmployeeId))
                .GroupBy(a => a.EmployeeId);
            foreach (var g in monthRecs)
                attByEmp[g.Key] = MonthlyAttendanceBuilder.Aggregate(g, holidaySet);
        }

        int created = 0, skipped = 0;
        decimal tGross = 0, tNet = 0, tEmpIns = 0, tEmprIns = 0, tTax = 0;
        var toAdd = new List<SalarySlip>();

        foreach (var e in emps)
        {
            if (existing.TryGetValue(e.Id, out var old))
            {
                if (!req.Overwrite) { skipped++; continue; }
                _slips.Remove(old);
            }

            // اضافه‌کاری/شب‌کاری/جمعه‌کاری و کسرِ غیبت از تجمیعِ تردد (در صورتِ فعال‌بودن).
            decimal otHours = 0, nightHours = 0, holidayHours = 0, absenceDeduction = 0;
            if (attByEmp.TryGetValue(e.Id, out var att))
            {
                otHours = att.OvertimeHours;
                nightHours = att.NightHours;
                holidayHours = att.HolidayHours;
                var dailyWage = e.BaseSalary / 30m;
                absenceDeduction = dailyWage * (att.AbsentDays + att.UnpaidLeaveDays);
            }

            var input = new FullPayrollInput(
                BaseSalary: e.BaseSalary,
                SeniorityBase: 0,
                HousingAllowance: set.HousingAllowance,
                FoodAllowance: set.FoodAllowance,
                Children: e.ChildrenCount,
                OvertimeHours: otHours,
                NightHours: nightHours,
                HolidayHours: holidayHours,
                AbsenceDeduction: absenceDeduction);
            var r = PayrollCalculator.ComputeFull(input, rates);

            var slip = SalarySlip.Create(e.Id, req.Year, req.Month,
                baseSalary: e.BaseSalary,
                overtimePay: r.OvertimePay,
                insuranceDeduct: r.EmployeeInsurance,
                taxDeduct: r.Tax,
                otherDeductions: absenceDeduction,
                housingAllowance: set.HousingAllowance,
                foodAllowance: set.FoodAllowance,
                childAllowance: r.ChildAllowance,
                nightShiftPay: r.NightPay,
                holidayPay: r.HolidayPay,
                employerInsurance: r.EmployerInsurance);

            toAdd.Add(slip);
            created++;
            tGross += r.Gross; tNet += r.Net; tEmpIns += r.EmployeeInsurance;
            tEmprIns += r.EmployerInsurance; tTax += r.Tax;
        }

        if (toAdd.Count > 0) await _slips.AddRangeAsync(toAdd, ct);
        await _uow.SaveChangesAsync(ct);

        return Result<RunPayrollResult>.Success(
            new RunPayrollResult(created, skipped, tGross, tNet, tEmpIns, tEmprIns, tTax));
    }
}
