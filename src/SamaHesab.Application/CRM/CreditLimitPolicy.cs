namespace SamaHesab.Application.CRM;

/// <summary>
/// کار #۳۷ — سیاست سقف اعتبار مشتری (منطق خالص و تست‌پذیر).
/// قاعده: اگر سقف اعتبار صفر باشد یعنی «بدون محدودیت» (رفتار فعلی حفظ می‌شود)؛
/// در غیر این صورت اگر بدهیِ پس از این فروش از سقف عبور کند، فروش نسیه مسدود می‌شود.
/// </summary>
public static class CreditLimitPolicy
{
    public static bool IsBlocked(decimal currentBalance, decimal newCreditAmount, decimal creditLimit)
        => creditLimit > 0 && newCreditAmount > 0 && (currentBalance + newCreditAmount) > creditLimit;

    /// <summary>اعتبار باقی‌مانده (برای نمایش). اگر سقف صفر باشد، نامحدود تلقی می‌شود.</summary>
    public static decimal Available(decimal currentBalance, decimal creditLimit)
        => creditLimit <= 0 ? decimal.MaxValue : creditLimit - currentBalance;
}
