using SamaHesab.Domain.Common;

namespace SamaHesab.Domain.Entities.HRM;

/// <summary>
/// PAY-C1-5 — تنظیماتِ سالِ حقوق (نرخ‌ها و مبالغِ پایه، قابلِ‌ویرایش هر سال).
/// یک ردیف برای هر شرکت+سالِ حقوقی. موتورِ محاسبه (`PayrollCalculator`) این مقادیر را به‌عنوانِ
/// نرخ‌های پارامتری می‌گیرد؛ مبالغِ ثابتِ ماهانه (حق‌مسکن/بن/اولاد/پایهٔ سنوات) هم اینجا تعریف می‌شوند.
/// </summary>
public class PayrollSetting : BaseEntity
{
    public int CompanyId { get; private set; }
    public string Year { get; private set; } = default!;            // سالِ حقوقیِ شمسی (مثلِ «۱۴۰۴»)

    // مبالغِ ثابتِ ماهانه (ریال)
    public decimal MinWageMonthly { get; private set; }             // حداقل حقوقِ ماهانه
    public decimal HousingAllowance { get; private set; }           // حق مسکن
    public decimal FoodAllowance { get; private set; }              // بن کارگری/خواربار
    public decimal ChildAllowancePerChild { get; private set; }     // حق اولاد به‌ازای هر فرزند
    public decimal MonthlyTaxExemption { get; private set; }        // معافیتِ ماهانهٔ مالیات

    // نرخ‌ها/ضرایب
    public decimal InsuranceEmployeeRate { get; private set; } = 0.07m;
    public decimal InsuranceEmployerRate { get; private set; } = 0.23m;
    public decimal HoursPerMonth { get; private set; } = 220m;
    public decimal OvertimeFactor { get; private set; } = 1.40m;
    public decimal HolidayFactor { get; private set; } = 1.40m;
    public decimal NightShiftFactor { get; private set; } = 0.35m;
    public int MaxChildren { get; private set; } = 2;

    private PayrollSetting() { }

    public static PayrollSetting Create(int companyId, string year)
    {
        if (string.IsNullOrWhiteSpace(year)) throw new ArgumentException("سالِ حقوقی الزامی است.");
        return new PayrollSetting { CompanyId = companyId, Year = year };
    }

    public void Update(decimal minWageMonthly, decimal housingAllowance, decimal foodAllowance,
        decimal childAllowancePerChild, decimal monthlyTaxExemption,
        decimal insuranceEmployeeRate, decimal insuranceEmployerRate, decimal hoursPerMonth,
        decimal overtimeFactor, decimal holidayFactor, decimal nightShiftFactor, int maxChildren)
    {
        MinWageMonthly = Nn(minWageMonthly);
        HousingAllowance = Nn(housingAllowance);
        FoodAllowance = Nn(foodAllowance);
        ChildAllowancePerChild = Nn(childAllowancePerChild);
        MonthlyTaxExemption = Nn(monthlyTaxExemption);
        InsuranceEmployeeRate = Nn(insuranceEmployeeRate);
        InsuranceEmployerRate = Nn(insuranceEmployerRate);
        HoursPerMonth = hoursPerMonth > 0 ? hoursPerMonth : 220m;
        OvertimeFactor = Nn(overtimeFactor);
        HolidayFactor = Nn(holidayFactor);
        NightShiftFactor = Nn(nightShiftFactor);
        MaxChildren = maxChildren < 0 ? 0 : maxChildren;
    }

    private static decimal Nn(decimal v) => v < 0 ? 0 : v;
}
