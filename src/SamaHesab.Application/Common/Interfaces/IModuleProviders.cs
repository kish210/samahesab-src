using SamaHesab.Domain.Entities.HRM;

namespace SamaHesab.Application.Common.Interfaces;

/// <summary>
/// قلابِ اختیاریِ هسته برای پورسانتِ فروش (پیاده‌سازی در ماژولِ Tourism). اگر ماژولِ گردشگری نصب نباشد،
/// این سرویس ثبت نمی‌شود و حقوق‌ومزد بدونِ پورسانت کار می‌کند (decouple از چرخهٔ HR↔Tourism).
/// </summary>
public interface ISalesCommissionProvider
{
    /// <summary>جمعِ پورسانتِ ماهِ شمسی به‌تفکیکِ کارمند (EmployeeId → مبلغ).</summary>
    Task<Dictionary<int, decimal>> CommissionByEmployeeAsync(
        IReadOnlyList<Employee> employees, int companyId, string persianYearMonth, CancellationToken ct);
}

/// <summary>
/// قلابِ اختیاریِ هسته برای هشدارِ ودیعهٔ کمِ تأمین‌کنندگانِ گردشگری (پیاده‌سازی در ماژولِ Tourism).
/// نبودِ ماژول → هشدار ساخته نمی‌شود.
/// </summary>
public interface ISupplierDepositAlertProvider
{
    Task<int> LowDepositCountAsync(CancellationToken ct);
}
