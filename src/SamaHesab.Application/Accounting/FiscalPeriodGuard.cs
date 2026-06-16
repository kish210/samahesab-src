using SamaHesab.Domain.Entities.Accounting;

namespace SamaHesab.Application.Accounting;

/// <summary>
/// فاز ۱۲ (RC) — قفلِ دورهٔ مالی: منطقِ خالص و مشترکِ همهٔ مسیرهای ثبت/تغییرِ سند
/// (سندِ دستی، دریافت/پرداختِ خزانه، قطعی‌سازی، برگشت). جلوگیری از ثبت در دورهٔ **بسته‌شده**
/// یا با تاریخِ **خارج از بازهٔ** سال مالی. تست‌پذیر و بدونِ وابستگی به UI/EF.
/// </summary>
public static class FiscalPeriodGuard
{
    /// <summary>
    /// در صورتِ مسدودبودن، پیامِ خطای فارسی برمی‌گرداند؛ در غیرِ این‌صورت <c>null</c>.
    /// نبودِ رکوردِ سال مالی (نصب‌های قدیمی) مجاز است تا چیزی نشکند.
    /// </summary>
    public static string? Check(FiscalYear? fy, string? date)
    {
        if (fy is null) return null;
        if (fy.IsClosed)
            return $"سال مالی «{fy.Title}» بسته شده است؛ ثبت/تغییرِ سند مجاز نیست.";
        if (!string.IsNullOrWhiteSpace(date) && !fy.Contains(date!))
            return $"تاریخ ({date}) خارج از بازهٔ سال مالی «{fy.Title}» ({fy.StartDate} تا {fy.EndDate}) است.";
        return null;
    }
}
