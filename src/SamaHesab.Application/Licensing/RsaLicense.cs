using System.Security.Cryptography;

namespace SamaHesab.Application.Licensing;

/// <summary>
/// فاز ۱۲ P-G7 — زیرساختِ امضای RSA لایسنس (آفلاین، SHA-256/PKCS#1).
/// <para><b>Sign</b> فقط سمتِ وندور (با کلیدِ خصوصی) استفاده می‌شود — کلیدِ خصوصی هرگز در کلاینت نیست.</para>
/// <para><b>Verify</b> در کلاینت با <b>کلیدِ عمومی</b> اجرا می‌شود (اعتبارسنجیِ آفلاین).</para>
/// </summary>
public static class RsaLicense
{
    /// <summary>امضای payload با کلیدِ خصوصی (PEM). فقط در ابزارِ صدورِ لایسنسِ وندور.</summary>
    public static string Sign(LicenseInfo info, string privateKeyPem)
    {
        using var rsa = RSA.Create();
        rsa.ImportFromPem(privateKeyPem);
        var sig = rsa.SignData(info.CanonicalBytes(), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return Convert.ToBase64String(sig);
    }

    /// <summary>تأییدِ امضای سند با کلیدِ عمومی (PEM). در کلاینت.</summary>
    public static bool Verify(LicenseDocument doc, string publicKeyPem)
    {
        if (doc is null || string.IsNullOrWhiteSpace(doc.Signature)) return false;
        try
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(publicKeyPem);
            var sig = Convert.FromBase64String(doc.Signature);
            return rsa.VerifyData(doc.License.CanonicalBytes(), sig, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        catch { return false; }   // graceful failure: امضای خراب/کلیدِ نامعتبر → نامعتبر
    }
}
