using SamaHesab.Domain.Common;

namespace SamaHesab.Domain.Entities.Settings;

/// <summary>
/// CR-X8 — تنظیماتِ شرکتیِ کلید-مقدار در DB (به‌جای فایلِ محلیِ هر ماشین) — برای همگامیِ چندایستگاهی.
/// چیزهای شرکت‌گستر (پروفایلِ شرکت، سیاست‌ها مثلِ EnforceSoD) این‌جا ذخیره می‌شوند؛ تنظیماتِ ماشین‌محلی
/// (رشتهٔ اتصال، آدرسِ API، Idle-timeout) همچنان در AppSettingsStore می‌مانند. یکتا بر شرکت+کلید.
/// </summary>
public class CompanySetting : AuditableEntity
{
    public string Key { get; private set; } = default!;
    public string? Value { get; private set; }

    private CompanySetting() { }

    public static CompanySetting Create(int companyId, string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("کلیدِ تنظیم الزامی است.");
        return new CompanySetting { CompanyId = companyId, Key = key, Value = value };
    }

    public void SetValue(string? value) { Value = value; SetAudit(null); }
}
