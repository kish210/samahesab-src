namespace SamaHesab.Application.Accounting;

/// <summary>
/// محاسبات توازن سند — منطق خالص و مشترک بین UI (توازن خودکار ردیف آخر) و سرویس‌ها.
/// </summary>
public static class VoucherBalance
{
    /// <summary>
    /// مبلغ بدهکار/بستانکارِ لازم برای متوازن‌کردن سند، بر اساس جمع‌های فعلی.
    /// اگر سند تراز باشد (0,0) برمی‌گرداند.
    /// </summary>
    public static (decimal Debit, decimal Credit) BalancingEntry(decimal totalDebit, decimal totalCredit)
    {
        var diff = totalDebit - totalCredit;
        if (diff > 0) return (0, diff);   // بدهکار بیشتر است → ردیف بستانکار لازم است
        if (diff < 0) return (-diff, 0);  // بستانکار بیشتر است → ردیف بدهکار لازم است
        return (0, 0);                    // متوازن
    }

    /// <summary>آیا با این جمع‌ها سند متوازن است؟ (با احتساب خطای گرد کردن)</summary>
    public static bool IsBalanced(decimal totalDebit, decimal totalCredit)
        => System.Math.Abs(totalDebit - totalCredit) < 0.01m;
}
