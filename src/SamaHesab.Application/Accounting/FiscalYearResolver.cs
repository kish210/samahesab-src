using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Domain.Interfaces.Repositories;

namespace SamaHesab.Application.Accounting;

/// <summary>
/// U-ACCT-1.7 — بسیاری از مسیرهایِ صدورِ سندِ خودکار (چک/انبار/فروش/رستوران) پیش‌تر
/// <c>FiscalYearId</c> را مستقیماً هاردکد رویِ ۱ می‌زدند — برایِ هر شرکتی که سالِ مالیِ فعالش
/// Id≠۱ باشد (مثلاً بعدِ از یک تعویضِ سالِ مالی)، سند به سالِ مالیِ اشتباه/بسته متصل می‌شد.
/// این کلاس منبعِ واحدِ «سالِ مالیِ فعالِ همین شرکت» است — تست‌پذیر و بدونِ وابستگی به UI/EF.
/// </summary>
public static class FiscalYearResolver
{
    /// <summary>
    /// شناسهٔ سالِ مالیِ <see cref="FiscalYear.IsActive"/> برایِ شرکت؛ اگر هیچ‌کدام فعال نبود،
    /// جدیدترین (بر اساسِ StartDate)؛ اگر اصلاً سالِ مالی‌ای ثبت نشده بود، ۱ (fallbackِ تاریخیِ
    /// همان هاردکدِ قبلی، فقط برایِ نصب‌هایِ خیلی قدیمی/ناقص که هنوز سالِ مالی seed نشده).
    /// </summary>
    public static async Task<int> ResolveActiveIdAsync(
        IRepository<FiscalYear> fiscalYears, int companyId, CancellationToken ct = default)
    {
        var years = await fiscalYears.FindAsync(f => f.CompanyId == companyId, ct);
        var active = years.FirstOrDefault(f => f.IsActive);
        if (active != null) return active.Id;
        var latest = years.OrderByDescending(f => f.StartDate).FirstOrDefault();
        return latest?.Id ?? 1;
    }
}
