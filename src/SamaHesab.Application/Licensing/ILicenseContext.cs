namespace SamaHesab.Application.Licensing;

/// <summary>
/// فاز ۱۲ P-G7 — وضعیتِ مؤثرِ لایسنس برای اعمالِ محدودیت‌ها در لایهٔ Application
/// (سقفِ شعبه/کاربرِ رده + سقفِ سندِ تریال). کلاینتِ دسکتاپ نسخهٔ واقعی را تزریق می‌کند؛
/// سرور/تست پیش‌فرضِ نامحدود می‌گیرند.
/// </summary>
public interface ILicenseContext
{
    bool IsTrial { get; }
    int MaxBranches { get; }          // LicenseLimits.Unlimited = نامحدود
    int MaxUsers { get; }
    int TrialVoucherLimit { get; }    // سقفِ سند در تریال (۲۰۰)
}

/// <summary>پیش‌فرضِ نامحدود (سرور/تست/نصبِ بدونِ کلاینتِ دسکتاپ).</summary>
public sealed class UnlimitedLicenseContext : ILicenseContext
{
    public bool IsTrial => false;
    public int MaxBranches => LicenseLimits.Unlimited;
    public int MaxUsers => LicenseLimits.Unlimited;
    public int TrialVoucherLimit => int.MaxValue;
}
