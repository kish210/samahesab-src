namespace SamaHesab.Application.Common.Interfaces;

/// <summary>
/// U-MULTI-COMPANY-1 — بعدِ ساختِ یک شرکتِ نو (چند شرکت در یک DBِ مشترک)، این سرویس
/// نمودارِ حساب/شعبه/انبارِ پیش‌فرضِ آن شرکت را seed می‌کند (بدونِ نیازِ ری‌استارتِ برنامه).
/// </summary>
public interface ICompanyProvisioningService
{
    Task ProvisionAsync(CancellationToken ct = default);
}
