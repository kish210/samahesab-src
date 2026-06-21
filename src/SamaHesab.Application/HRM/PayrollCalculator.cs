namespace SamaHesab.Application.HRM;

/// <summary>ورودیِ محاسبهٔ یک فیشِ حقوقی (نسخهٔ پایه — حفظِ سازگاری).</summary>
public record PayrollInput(decimal BaseSalary, decimal Overtime = 0, decimal Allowances = 0);

/// <summary>نتیجهٔ محاسبهٔ فیشِ حقوقی (نسخهٔ پایه — حفظِ سازگاری).</summary>
public record PayrollResult(decimal Gross, decimal Insurance, decimal Tax, decimal Net);

// ─────────────────────────── PAY-C2-1: موتورِ کاملِ حقوقِ ایرانی ───────────────────────────

/// <summary>
/// نرخ‌ها و ثابت‌های قابلِ‌پیکربندیِ حقوق (هر سال در «تنظیماتِ سالِ حقوق» قابلِ‌تغییر).
/// مقادیرِ پیش‌فرض تقریبیِ ۱۴۰۴‌اند و باید پیش از استفادهٔ رسمی با مصوبهٔ همان سال تطبیق یابند.
/// </summary>
public record PayrollRates(
    decimal InsuranceEmployeeRate = 0.07m,   // سهمِ کارمند
    decimal InsuranceEmployerRate = 0.23m,   // سهمِ کارفرما (۲۰٪ + ۳٪ بیکاری)
    decimal MonthlyTaxExemption   = 100_000_000m,  // معافیتِ ماهانهٔ مالیات (ریال)
    decimal HoursPerMonth         = 220m,    // ساعتِ کارِ ماهانه (مبنای نرخِ ساعتی)
    decimal OvertimeFactor        = 1.40m,   // ضریبِ اضافه‌کاری
    decimal HolidayFactor         = 1.40m,   // ضریبِ جمعه/تعطیل‌کاری
    decimal NightShiftFactor      = 0.35m,   // فوق‌العادهٔ شب‌کاری (۳۵٪)
    decimal ChildAllowancePerChild = 0m,     // حق اولاد به‌ازای هر فرزند (تا سقفِ MaxChildren)
    int MaxChildren               = 2);

/// <summary>ورودیِ محاسبهٔ کاملِ یک فیشِ حقوقیِ ماهانه.</summary>
public record FullPayrollInput(
    decimal BaseSalary,            // حقوقِ پایهٔ ماهانه
    decimal SeniorityBase = 0,     // پایهٔ سنوات
    decimal HousingAllowance = 0,  // حق مسکن
    decimal FoodAllowance = 0,     // بن/خواربار
    int Children = 0,              // تعدادِ فرزندِ مشمولِ حق اولاد
    decimal OvertimeHours = 0,     // ساعتِ اضافه‌کاری
    decimal NightHours = 0,        // ساعتِ شب‌کاری
    decimal HolidayHours = 0,      // ساعتِ جمعه/تعطیل‌کاری
    decimal MissionAmount = 0,     // مأموریت/سایرِ مزایای مشمول
    decimal AdvanceDeduction = 0,  // مساعده/وام
    decimal AbsenceDeduction = 0,  // کسرِ غیبت/تأخیر
    decimal OtherDeductions = 0,   // سایرِ کسورات
    decimal OtherEarnings = 0);    // سایرِ پرداختی‌های مشمول

/// <summary>نتیجهٔ محاسبهٔ کاملِ فیشِ حقوقی — همهٔ اجزا تفکیک‌شده.</summary>
public record FullPayrollResult(
    decimal OvertimePay, decimal NightPay, decimal HolidayPay, decimal ChildAllowance,
    decimal Gross,              // ناخالص = همهٔ پرداختی‌ها
    decimal InsurableBase,      // مشمولِ بیمه (بدونِ حق اولاد)
    decimal EmployeeInsurance,  // بیمهٔ سهمِ کارمند
    decimal EmployerInsurance,  // بیمهٔ سهمِ کارفرما (تعهدِ کارفرما — جزءِ کسورات نیست)
    decimal Tax,                // مالیاتِ حقوق
    decimal TotalDeductions,    // کلِ کسورات (بیمهٔ کارمند + مالیات + مساعده + غیبت + سایر)
    decimal Net);               // خالصِ پرداختی

/// <summary>
/// موتورِ خالصِ محاسبهٔ حقوق — مستقل و تست‌پذیر، بدونِ وابستگی به DB.
/// نسخهٔ پایهٔ <see cref="Compute"/> حفظ شده؛ <see cref="ComputeFull"/> همهٔ اجزای قانونِ کار را پوشش می‌دهد.
/// </summary>
public static class PayrollCalculator
{
    public const decimal InsuranceEmployeeRate = 0.07m;

    // ── نسخهٔ پایه (سازگاریِ عقب‌رو) ──
    public static PayrollResult Compute(PayrollInput input, decimal monthlyTaxExemption = 10_000_000m)
    {
        var gross = Round(input.BaseSalary + input.Overtime + input.Allowances);
        if (gross <= 0) return new PayrollResult(0, 0, 0, 0);

        var insurance = Round(gross * InsuranceEmployeeRate);
        var taxable = gross - insurance;
        var tax = Round(ComputeTax(taxable, monthlyTaxExemption));
        var net = gross - insurance - tax;
        return new PayrollResult(gross, insurance, tax, net);
    }

    /// <summary>محاسبهٔ کاملِ فیشِ حقوقیِ ماهانه با همهٔ اجزا و نرخ‌های پارامتری.</summary>
    public static FullPayrollResult ComputeFull(FullPayrollInput i, PayrollRates? rates = null)
    {
        var r = rates ?? new PayrollRates();
        var hourly = r.HoursPerMonth > 0 ? (i.BaseSalary + i.SeniorityBase) / r.HoursPerMonth : 0m;

        var overtimePay = Round(hourly * r.OvertimeFactor   * Math.Max(0, i.OvertimeHours));
        var nightPay    = Round(hourly * r.NightShiftFactor * Math.Max(0, i.NightHours));
        var holidayPay  = Round(hourly * r.HolidayFactor    * Math.Max(0, i.HolidayHours));
        var children    = Math.Max(0, Math.Min(i.Children, r.MaxChildren));
        var childAllow  = Round(r.ChildAllowancePerChild * children);

        // مشمولِ بیمه = همهٔ پرداختی‌ها به‌جز حق اولاد (که معاف از بیمه است).
        var insurableBase = Round(i.BaseSalary + i.SeniorityBase + i.HousingAllowance + i.FoodAllowance
                                  + overtimePay + nightPay + holidayPay + i.MissionAmount + i.OtherEarnings);
        var gross = Round(insurableBase + childAllow);
        if (gross <= 0)
            return new FullPayrollResult(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

        var empInsurance   = Round(insurableBase * r.InsuranceEmployeeRate);
        var employerInsur  = Round(insurableBase * r.InsuranceEmployerRate);
        var taxable        = gross - empInsurance;
        var tax            = Round(ComputeTax(taxable, r.MonthlyTaxExemption));

        var totalDeductions = empInsurance + tax
                              + Math.Max(0, i.AdvanceDeduction)
                              + Math.Max(0, i.AbsenceDeduction)
                              + Math.Max(0, i.OtherDeductions);
        var net = gross - totalDeductions;

        return new FullPayrollResult(overtimePay, nightPay, holidayPay, childAllow,
            gross, insurableBase, empInsurance, employerInsur, tax, Round(totalDeductions), net);
    }

    private static decimal ComputeTax(decimal taxable, decimal exemption)
    {
        if (exemption <= 0) exemption = 10_000_000m;
        if (taxable <= exemption) return 0m;

        var brackets = new (decimal upTo, decimal rate)[]
        {
            (exemption * 1.5m, 0.10m),
            (exemption * 2.5m, 0.15m),
            (exemption * 3.5m, 0.20m),
            (decimal.MaxValue,  0.25m),
        };

        decimal tax = 0, lower = exemption;
        foreach (var (upTo, rate) in brackets)
        {
            if (taxable <= lower) break;
            var slice = Math.Min(taxable, upTo) - lower;
            if (slice > 0) tax += slice * rate;
            lower = upTo;
        }
        return tax;
    }

    private static decimal Round(decimal v) => Math.Round(v, 0, MidpointRounding.AwayFromZero);
}
