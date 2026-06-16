using SamaHesab.Application.Licensing;

namespace SamaHesab.WPF.Services;

/// <summary>
/// فاز ۱۲ P-G7 — نسخهٔ واقعیِ <see cref="ILicenseContext"/> در کلاینتِ دسکتاپ، بر پایهٔ <see cref="LicenseService"/>.
/// فعال‌شده → سقفِ رده؛ تریال → شعبه/کاربرِ نامحدود ولی سقفِ ۲۰۰ سند.
/// </summary>
public sealed class WpfLicenseContext : ILicenseContext
{
    private readonly LicenseService _license;
    public WpfLicenseContext(LicenseService license) => _license = license;

    private AppLicenseStatus S => _license.GetStatus();

    public bool IsTrial => S.State == AppLicenseState.Trial;

    public int MaxBranches => S is { State: AppLicenseState.Activated, License: { } lic }
        ? lic.MaxBranches : LicenseLimits.Unlimited;   // تریال: نامحدود (محدودیتِ زمان/سند)

    public int MaxUsers => S is { State: AppLicenseState.Activated, License: { } lic }
        ? lic.MaxUsers : LicenseLimits.Unlimited;

    public int TrialVoucherLimit => TrialPolicy.MaxVouchers;
}
