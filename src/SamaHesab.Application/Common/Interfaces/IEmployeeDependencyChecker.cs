namespace SamaHesab.Application.Common.Interfaces;

/// <summary>
/// قراردادِ بررسیِ وابستگیِ کارمند برای حذفِ امن — هسته فقط این اینترفیس را می‌شناسد، نه ماژول‌ها.
/// ماژولِ HR آن را پیاده می‌کند (سابقهٔ فیشِ حقوق/تردد). اگر ماژولِ HR نصب/فعال نباشد، هیچ
/// پیاده‌سازی‌ای ثبت نمی‌شود ⇒ کارمند بدونِ سابقه است و حذفِ سخت امن می‌ماند (هسته سالم).
/// </summary>
public interface IEmployeeDependencyChecker
{
    /// <summary>آیا این کارمند سابقه‌ای دارد که حذفِ سختش را ممنوع کند (و باید غیرفعال شود)؟</summary>
    Task<bool> HasHistoryAsync(int employeeId, CancellationToken ct = default);
}
