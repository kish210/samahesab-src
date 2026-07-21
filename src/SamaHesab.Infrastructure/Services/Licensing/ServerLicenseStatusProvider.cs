using Microsoft.Extensions.Configuration;
using SamaHesab.Application.Licensing;

namespace SamaHesab.Infrastructure.Services.Licensing;

/// <summary>
/// می‌خواند `Server:InstalledUtc` را (نصاب در appsettings.Production.json در لحظهٔ نصب می‌نویسد).
/// اگر کلید نبود (نصبِ قدیمی‌تر، یا محیطِ توسعه) — به‌جایِ خطا/انقضایِ اشتباه، وضعیتِ «نامحدود/بدونِ
/// بازه» برمی‌گردد؛ یعنی مشتریانِ فعلی که این کلید را ندارند هرگز به‌اشتباه «منقضی» دیده نمی‌شوند.
/// </summary>
public sealed class ServerLicenseStatusProvider : IServerLicenseStatusProvider
{
    private readonly IConfiguration _config;
    public ServerLicenseStatusProvider(IConfiguration config) { _config = config; }

    public ServerLicenseStatusDto GetStatus()
    {
        var raw = _config["Server:InstalledUtc"];
        if (!DateTime.TryParse(raw, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal,
                out var installedUtc))
        {
            return new ServerLicenseStatusDto("Enterprise", null, null, false, null);
        }

        var expiresUtc = installedUtc.AddYears(1);
        var now = DateTime.UtcNow;
        var isExpired = now > expiresUtc;
        var daysRemaining = isExpired ? 0 : (int)Math.Ceiling((expiresUtc - now).TotalDays);
        return new ServerLicenseStatusDto("Enterprise", installedUtc, expiresUtc, isExpired, daysRemaining);
    }
}
