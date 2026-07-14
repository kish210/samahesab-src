using SamaHesab.Domain.Common;

namespace SamaHesab.Modules.TaxInvoicing.Domain;

/// <summary>
/// تنظیماتِ سامانهٔ مودیانِ هر شرکت — یک ردیف به‌ازای هر شرکت (هم‌الگو با <c>TourismSetting</c>).
/// گواهیِ دیجیتال (کلیدِ خصوصیِ RSA + زنجیرهٔ X.509) که سازمان هنگامِ ثبت‌نام صادر می‌کند، به‌صورتِ
/// فایلِ PFX روی دیسک نگه‌داری می‌شود؛ اینجا فقط مسیر/رمزِ عبورش ذخیره می‌شود (نه خودِ کلید).
/// </summary>
public class ModianSettings : AuditableEntity
{
    /// <summary>شناسهٔ حافظهٔ مالیاتی (Tax Memory ID) که سازمان صادر می‌کند.</summary>
    public string? TaxMemoryId { get; private set; }
    public bool UseSandbox { get; private set; } = true;
    public string? CertificatePath { get; private set; }
    public string? CertificatePassword { get; private set; }
    public bool Enabled { get; private set; }

    private ModianSettings() { }

    public static ModianSettings Create(int companyId) => new() { CompanyId = companyId };

    public void Update(string? taxMemoryId, bool useSandbox, string? certificatePath,
        string? certificatePassword, bool enabled)
    {
        TaxMemoryId = taxMemoryId?.Trim();
        UseSandbox = useSandbox;
        CertificatePath = certificatePath?.Trim();
        CertificatePassword = certificatePassword;
        Enabled = enabled;
        SetAudit(null);
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(TaxMemoryId) && !string.IsNullOrWhiteSpace(CertificatePath);
}
