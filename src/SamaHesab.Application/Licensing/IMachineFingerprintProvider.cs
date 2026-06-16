namespace SamaHesab.Application.Licensing;

/// <summary>
/// فاز ۱۲ P-G7 — اثرِانگشتِ سخت‌افزاریِ دستگاه (قفلِ ماشین).
/// پیاده‌سازی در Infrastructure: ترکیبِ شناسهٔ مادربرد + Windows MachineGuid + شناسهٔ CPU
/// (نه فقط MAC). برنامه فقط این رشته را با اثرِانگشتِ داخلِ لایسنس مقایسه می‌کند.
/// </summary>
public interface IMachineFingerprintProvider
{
    /// <summary>اثرِانگشتِ پایدارِ این دستگاه (هشِ Hex).</summary>
    string GetFingerprint();
}
