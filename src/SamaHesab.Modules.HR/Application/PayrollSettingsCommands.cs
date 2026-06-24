using MediatR;
using SamaHesab.Application.Common.Interfaces;
using SamaHesab.Application.Common.Models;
using SamaHesab.Domain.Entities.HRM;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.HRM;

/// <summary>PAY-C1-5 — تنظیماتِ سالِ حقوق (نرخ‌ها و مبالغِ پایه).</summary>
public record PayrollSettingsDto(
    string Year,
    decimal MinWageMonthly, decimal HousingAllowance, decimal FoodAllowance,
    decimal ChildAllowancePerChild, decimal MonthlyTaxExemption,
    decimal InsuranceEmployeeRate, decimal InsuranceEmployerRate, decimal HoursPerMonth,
    decimal OvertimeFactor, decimal HolidayFactor, decimal NightShiftFactor, int MaxChildren)
{
    /// <summary>تبدیل به نرخ‌های پارامتریِ موتورِ محاسبه.</summary>
    public PayrollRates ToRates() => new(
        InsuranceEmployeeRate, InsuranceEmployerRate, MonthlyTaxExemption, HoursPerMonth,
        OvertimeFactor, HolidayFactor, NightShiftFactor, ChildAllowancePerChild, MaxChildren);

    /// <summary>پیش‌فرضِ معقول وقتی هنوز تنظیماتی ذخیره نشده.</summary>
    public static PayrollSettingsDto Default(string year) => new(
        year, 0, 0, 0, 0, 100_000_000m, 0.07m, 0.23m, 220m, 1.40m, 1.40m, 0.35m, 2);

    public static PayrollSettingsDto From(PayrollSetting s) => new(
        s.Year, s.MinWageMonthly, s.HousingAllowance, s.FoodAllowance, s.ChildAllowancePerChild,
        s.MonthlyTaxExemption, s.InsuranceEmployeeRate, s.InsuranceEmployerRate, s.HoursPerMonth,
        s.OvertimeFactor, s.HolidayFactor, s.NightShiftFactor, s.MaxChildren);
}

// ── خواندن ──
public record GetPayrollSettingsQuery(string Year) : IRequest<PayrollSettingsDto>;

public class GetPayrollSettingsQueryHandler : IRequestHandler<GetPayrollSettingsQuery, PayrollSettingsDto>
{
    private readonly IRepository<PayrollSetting> _settings;
    private readonly ICurrentUserService _user;

    public GetPayrollSettingsQueryHandler(IRepository<PayrollSetting> settings, ICurrentUserService user)
    { _settings = settings; _user = user; }

    public async Task<PayrollSettingsDto> Handle(GetPayrollSettingsQuery req, CancellationToken ct)
    {
        var companyId = _user.CompanyId ?? 1;
        var row = await _settings.FindSingleAsync(s => s.CompanyId == companyId && s.Year == req.Year, ct);
        return row is null ? PayrollSettingsDto.Default(req.Year) : PayrollSettingsDto.From(row);
    }
}

// ── ذخیره (upsert) ──
public record SavePayrollSettingsCommand(PayrollSettingsDto Settings) : IRequest<Result<int>>;

public class SavePayrollSettingsCommandHandler : IRequestHandler<SavePayrollSettingsCommand, Result<int>>
{
    private readonly IRepository<PayrollSetting> _settings;
    private readonly IUnitOfWork _uow;
    private readonly ICurrentUserService _user;

    public SavePayrollSettingsCommandHandler(IRepository<PayrollSetting> settings, IUnitOfWork uow, ICurrentUserService user)
    { _settings = settings; _uow = uow; _user = user; }

    public async Task<Result<int>> Handle(SavePayrollSettingsCommand req, CancellationToken ct)
    {
        var d = req.Settings;
        if (string.IsNullOrWhiteSpace(d.Year)) return Result<int>.Failure("سالِ حقوقی الزامی است.");
        var companyId = _user.CompanyId ?? 1;

        var row = await _settings.FindSingleAsync(s => s.CompanyId == companyId && s.Year == d.Year, ct);
        var isNew = row is null;
        row ??= PayrollSetting.Create(companyId, d.Year);
        row.Update(d.MinWageMonthly, d.HousingAllowance, d.FoodAllowance, d.ChildAllowancePerChild,
            d.MonthlyTaxExemption, d.InsuranceEmployeeRate, d.InsuranceEmployerRate, d.HoursPerMonth,
            d.OvertimeFactor, d.HolidayFactor, d.NightShiftFactor, d.MaxChildren);

        if (isNew) await _settings.AddAsync(row, ct);
        await _uow.SaveChangesAsync(ct);
        return Result<int>.Success(row.Id);
    }
}
