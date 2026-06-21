namespace SamaHesab.Application.HRM;

/// <summary>قواعدِ استحقاقِ مرخصی (قابلِ‌پیکربندی).</summary>
public record LeaveRules(
    decimal AnnualEntitlementDays = 26m,  // استحقاقِ سالانهٔ مرخصیِ استحقاقی (روزِ کاری)
    decimal WorkHoursPerDay = 7.33m,      // ساعتِ کاریِ یک روز (برای تبدیلِ مرخصیِ ساعتی)
    decimal MaxCarryOverDays = 9m);       // سقفِ انتقالِ ماندهٔ سال‌قبل (۹ روز)

/// <summary>مصرفِ مرخصی در یک دوره.</summary>
public record LeaveUsage(
    decimal UsedDays = 0,     // روزهای مرخصیِ کامل
    decimal UsedHours = 0);   // ساعت‌های مرخصیِ ساعتی

/// <summary>ماندهٔ مرخصی (روز و ساعت).</summary>
public record LeaveBalance(
    decimal EntitlementDays,  // استحقاقِ این دوره (شاملِ انتقالی)
    decimal UsedDays,         // مصرفِ معادلِ روز
    decimal RemainingDays,    // ماندهٔ روز
    decimal RemainingHours);  // ماندهٔ معادلِ ساعت

/// <summary>
/// موتورِ خالصِ ماندهٔ مرخصی — مستقل و تست‌پذیر، بدونِ وابستگی به DB.
/// استحقاقِ سالانه/ماهانه + انتقالیِ سال‌قبل (سقف‌دار) − مصرف = مانده.
/// </summary>
public static class LeaveBalanceCalculator
{
    /// <summary>استحقاقِ ماهانه = استحقاقِ سالانه ÷ ۱۲ (برای محاسبهٔ تجمیعی تا یک ماه).</summary>
    public static decimal MonthlyAccrual(LeaveRules? rules = null)
    {
        var r = rules ?? new LeaveRules();
        return Round(r.AnnualEntitlementDays / 12m);
    }

    /// <summary>
    /// محاسبهٔ ماندهٔ مرخصی تا پایانِ یک ماهِ مشخص از سال (۱..۱۲).
    /// </summary>
    /// <param name="carryOverDays">ماندهٔ سال‌قبل (پیش از اعمالِ سقف).</param>
    public static LeaveBalance Compute(int monthOfYear, LeaveUsage used,
        decimal carryOverDays = 0, LeaveRules? rules = null)
    {
        var r = rules ?? new LeaveRules();
        var month = Math.Clamp(monthOfYear, 0, 12);

        var carry = Math.Clamp(carryOverDays, 0, r.MaxCarryOverDays);
        var accruedThisYear = Round(r.AnnualEntitlementDays / 12m * month);
        var entitlement = Round(carry + accruedThisYear);

        var usedDays = Round(Math.Max(0, used.UsedDays)
                             + (r.WorkHoursPerDay > 0 ? Math.Max(0, used.UsedHours) / r.WorkHoursPerDay : 0));

        var remainingDays = Round(entitlement - usedDays);
        var remainingHours = Round(remainingDays * r.WorkHoursPerDay);

        return new LeaveBalance(entitlement, usedDays, remainingDays, remainingHours);
    }

    /// <summary>آیا درخواستِ مرخصیِ جدید (روز) از ماندهٔ موجود بیشتر است؟</summary>
    public static bool CanRequest(LeaveBalance balance, decimal requestDays) =>
        requestDays > 0 && requestDays <= balance.RemainingDays;

    private static decimal Round(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
}
