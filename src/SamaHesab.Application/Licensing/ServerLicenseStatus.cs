namespace SamaHesab.Application.Licensing;

/// <summary>
/// وضعیتِ لایسنسِ نصبِ سرور/وب — صرفاً اطلاع‌رسانی است، نه یک دروازهٔ فنی: نصبِ web-setup.iss
/// تاریخِ خودش را (`Server:InstalledUtc` در appsettings.Production.json) در لحظهٔ نصب مُهر می‌زند
/// تا یک‌سال رایگانِ کامل (Enterprise/همهٔ ماژول‌ها) از همان لحظه شمرده شود، بدونِ نیازِ کاربر به
/// واردکردنِ فایلِ لایسنس. بعدِ اتمامِ بازه هیچ قابلیتی قفل نمی‌شود (چون جریانِ پرداخت/تمدید هنوز
/// ساخته نشده) — فقط یک بنرِ اطلاع‌رسانی در وب دیده می‌شود.
/// </summary>
public record ServerLicenseStatusDto(string Tier, DateTime? InstalledUtc, DateTime? ExpiresUtc, bool IsExpired, int? DaysRemaining);

public interface IServerLicenseStatusProvider
{
    ServerLicenseStatusDto GetStatus();
}
