namespace SamaHesab.Domain.Entities.Accounting;

/// <summary>
/// موتورِ خالصِ جدولِ اقساطِ وام (قسطِ مساوی/annuity) — بدونِ وابستگی به DB/تقویم، قابلِ تستِ واحد.
/// </summary>
public static class LoanCalculator
{
    public record Installment(int Index, decimal Payment, decimal Principal, decimal Interest, decimal Remaining);

    /// <summary>نرخِ بهرهٔ ماهانه از درصدِ سالانه.</summary>
    public static decimal MonthlyRate(decimal annualPercent) => annualPercent / 100m / 12m;

    /// <summary>مبلغِ قسطِ ثابت (annuity). بدونِ بهره → اصل ÷ تعداد.</summary>
    public static decimal EqualPayment(decimal principal, decimal annualPercent, int termMonths)
    {
        if (principal <= 0 || termMonths <= 0) return 0;
        var r = MonthlyRate(annualPercent);
        if (r <= 0) return principal / termMonths;
        var factor = (decimal)Math.Pow((double)(1 + r), -termMonths);
        return principal * r / (1 - factor);
    }

    /// <summary>جدولِ کاملِ اقساط: بهره روی ماندهٔ هر دوره، اصل = قسط − بهره؛ قسطِ آخر مانده را صفر می‌کند.</summary>
    public static List<Installment> BuildSchedule(decimal principal, decimal annualPercent, int termMonths)
    {
        var result = new List<Installment>();
        if (principal <= 0 || termMonths <= 0) return result;

        var r = MonthlyRate(annualPercent);
        var payment = EqualPayment(principal, annualPercent, termMonths);
        var remaining = principal;

        for (var i = 1; i <= termMonths; i++)
        {
            var interest = Math.Round(remaining * r, 2);
            var principalPart = payment - interest;

            // قسطِ آخر: صاف‌کردنِ باقی‌ماندهٔ ناشی از گردکردن.
            if (i == termMonths)
            {
                principalPart = remaining;
                interest = payment - principalPart;
                if (interest < 0) interest = 0;
                payment = principalPart + interest;
            }

            principalPart = Math.Round(principalPart, 2);
            payment = Math.Round(payment, 2);
            interest = Math.Round(interest, 2);
            remaining = Math.Max(0, Math.Round(remaining - principalPart, 2));

            result.Add(new Installment(i, payment, principalPart, interest, remaining));
        }
        return result;
    }
}
