using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using SamaHesab.Modules.TaxInvoicing.Domain;

namespace SamaHesab.Modules.TaxInvoicing.Crypto;

/// <summary>
/// بارگذاریِ کلیدِ خصوصیِ RSAِ امضا از فایلِ PFXِ گواهیِ دیجیتال — جدا از <see cref="ModianCryptoService"/>
/// نگه‌داشته شده تا در تست بتوان بدونِ فایلِ واقعیِ گواهی جایگزینش کرد (تستِ خالصِ رمزنگاری،
/// بدونِ I/O دیسک).
/// </summary>
public interface IModianCertificateProvider
{
    RSA LoadSigningKey(ModianSettings settings);
}

public sealed class ModianCertificateProvider : IModianCertificateProvider
{
    public RSA LoadSigningKey(ModianSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.CertificatePath))
            throw new InvalidOperationException("مسیرِ فایلِ گواهیِ دیجیتال تنظیم نشده است.");

        using var cert = new X509Certificate2(
            settings.CertificatePath, settings.CertificatePassword,
            X509KeyStorageFlags.Exportable | X509KeyStorageFlags.EphemeralKeySet);
        var key = cert.GetRSAPrivateKey();
        return key ?? throw new InvalidOperationException("فایلِ گواهی فاقدِ کلیدِ خصوصیِ RSA است.");
    }
}
