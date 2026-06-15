namespace SamaHesab.Application.HRM;

/// <summary>ورودیِ محاسبهٔ یک فیشِ حقوقی.</summary>
public record PayrollInput(decimal BaseSalary, decimal Overtime = 0, decimal Allowances = 0);

/// <summary>نتیجهٔ محاسبهٔ فیشِ حقوقی.</summary>
public record PayrollResult(decimal Gross, decimal Insurance, decimal Tax, decimal Net);

/// <summary>
/// موتورِ خالصِ محاسبهٔ حقوق (HR/M7) — مستقل و تست‌پذیر، بدونِ وابستگی به DB.
/// مدلِ ساده‌شدهٔ ایران:
///   • ناخالص = پایه + اضافه‌کاری + مزایا.
///   • بیمهٔ سهمِ کارمند = ۷٪ ناخالص.
///   • مالیاتِ حقوق پلکانی روی «ناخالص − بیمه» با معافیتِ ماهانه (پیش‌فرض ۱۰٬۰۰۰٬۰۰۰ ریال):
///       تا معافیت: ۰٪ · تا ۱.۵×معافیت: ۱۰٪ · تا ۲.۵×: ۱۵٪ · تا ۳.۵×: ۲۰٪ · مازاد: ۲۵٪.
///   • خالص = ناخالص − بیمه − مالیات.
/// نرخ‌ها ثابتِ نسخهٔ پایه‌اند؛ پارامتریک‌سازی (جدولِ نرخ) فازِ بعد.
/// </summary>
public static class PayrollCalculator
{
    public const decimal InsuranceEmployeeRate = 0.07m;

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
