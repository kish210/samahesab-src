namespace SamaHesab.Application.Licensing;

public enum TrialState { Active, Expired }

/// <summary>
/// فاز ۱۲ P-G7 — سیاستِ نسخهٔ آزمایشی: <b>۱۲۰ روز</b> یا <b>۲۰۰ سندِ حسابداری</b> — هرکدام زودتر.
/// کاملاً آفلاین (تاریخِ نصب + شمارشِ سندِ موجود در DB).
/// </summary>
public static class TrialPolicy
{
    public const int TrialDays = 120;
    public const int MaxVouchers = 200;

    public static (TrialState State, int DaysRemaining, int VouchersRemaining) Evaluate(
        DateTime installUtc, DateTime nowUtc, int voucherCount)
    {
        var daysUsed = (int)System.Math.Floor((nowUtc.Date - installUtc.Date).TotalDays);
        var daysRem = System.Math.Max(0, TrialDays - daysUsed);
        var vouchersRem = System.Math.Max(0, MaxVouchers - voucherCount);
        var state = (daysRem <= 0 || vouchersRem <= 0) ? TrialState.Expired : TrialState.Active;
        return (state, daysRem, vouchersRem);
    }
}
