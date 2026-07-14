using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace SamaHesab.Modules.TaxInvoicing.Crypto;

/// <summary>
/// پیاده‌سازیِ سبکِ JWS/JWEِ compact serialization (RFC 7515/7516) با <c>System.Security.Cryptography</c>ِ
/// داخلیِ .NET — بدونِ نیازِ NuGetِ JOSE (هیچ‌کدام در سالوشن نیست). امضا با RS256، رمزنگاری با
/// RSA-OAEP-256 (کلیدِ محتوا) + A256GCM (خودِ بدنه)، عیناً همان الگویِ RSA-signِ موجود در
/// <c>RsaLicense.cs</c>.
/// ⚠️ ادعاهایِ ساختاریِ JOSE (RFC7515/7516) استاندارد و مطمئن‌اند؛ ولی جزئیاتِ **هدرهایِ اختصاصیِ**
/// سامانهٔ مودیان (مثلِ <c>x5c</c>/<c>sigT</c>/<c>crit</c> که در تحقیقِ اولیه دیده شد) از یک منبعِ فنیِ
/// غیررسمی آمده و اینجا پیاده نشده — <see cref="CreateJws"/> پارامترِ <c>extraHeaderClaims</c> را برایِ
/// افزودنِ همان‌ها هنگامِ دریافتِ اعتبارنامهٔ واقعی/مستنداتِ رسمی می‌گیرد.
/// </summary>
public interface IModianCryptoService
{
    /// <summary>امضایِ RS256 روی <paramref name="payloadJson"/> → JWS به‌صورتِ compact serialization.</summary>
    string CreateJws(string payloadJson, RSA signingKey, IReadOnlyDictionary<string, object?>? extraHeaderClaims = null);

    /// <summary>تأییدِ امضا و برگرداندنِ بدنهٔ JSONِ اصلی؛ در صورتِ نامعتبربودنِ امضا استثنا می‌زند.</summary>
    string VerifyAndExtractJwsPayload(string jws, RSA verificationKey);

    /// <summary>رمزنگاریِ <paramref name="payloadJson"/> با RSA-OAEP-256 (کلیدِ محتوایِ تصادفی) + A256GCM → JWE.</summary>
    string CreateJwe(string payloadJson, RSA recipientPublicKey);

    /// <summary>رمزگشاییِ JWE — فقط برایِ تستِ round-trip با کلیدِ آزمایشیِ خودمان (سرورِ واقعی هرگز کلیدِ خصوصیِ خودش را به ما نمی‌دهد).</summary>
    string DecryptJwe(string jwe, RSA recipientPrivateKey);
}

public sealed class ModianCryptoService : IModianCryptoService
{
    public string CreateJws(string payloadJson, RSA signingKey, IReadOnlyDictionary<string, object?>? extraHeaderClaims = null)
    {
        var header = new Dictionary<string, object?> { ["alg"] = "RS256", ["typ"] = "JOSE" };
        if (extraHeaderClaims != null)
            foreach (var kv in extraHeaderClaims) header[kv.Key] = kv.Value;

        var headerB64 = Base64Url(JsonSerializer.SerializeToUtf8Bytes(header));
        var payloadB64 = Base64Url(Encoding.UTF8.GetBytes(payloadJson));
        var signingInput = Encoding.ASCII.GetBytes($"{headerB64}.{payloadB64}");
        var signature = signingKey.SignData(signingInput, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return $"{headerB64}.{payloadB64}.{Base64Url(signature)}";
    }

    public string VerifyAndExtractJwsPayload(string jws, RSA verificationKey)
    {
        var parts = jws.Split('.');
        if (parts.Length != 3) throw new FormatException("JWSِ compact باید دقیقاً سه بخش داشته باشد.");

        var signingInput = Encoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}");
        var signature = Base64UrlDecode(parts[2]);
        if (!verificationKey.VerifyData(signingInput, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
            throw new CryptographicException("امضایِ JWS نامعتبر است.");

        return Encoding.UTF8.GetString(Base64UrlDecode(parts[1]));
    }

    public string CreateJwe(string payloadJson, RSA recipientPublicKey)
    {
        var header = new Dictionary<string, object?> { ["alg"] = "RSA-OAEP-256", ["enc"] = "A256GCM" };
        var headerB64 = Base64Url(JsonSerializer.SerializeToUtf8Bytes(header));
        // RFC 7516 — protected headerِ base64urlِ ASCII به‌عنوانِ AAD (Additional Authenticated Data) به AES-GCM می‌رود.
        var aad = Encoding.ASCII.GetBytes(headerB64);

        var cek = RandomNumberGenerator.GetBytes(32);   // کلیدِ محتوایِ ۲۵۶بیتی برایِ A256GCM
        var encryptedKey = recipientPublicKey.Encrypt(cek, RSAEncryptionPadding.OaepSHA256);

        var iv = RandomNumberGenerator.GetBytes(12);     // IVِ ۹۶بیتیِ استانداردِ GCM
        var plaintext = Encoding.UTF8.GetBytes(payloadJson);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[16];
        using (var aesGcm = new AesGcm(cek, tag.Length))
            aesGcm.Encrypt(iv, plaintext, ciphertext, tag, aad);

        return $"{headerB64}.{Base64Url(encryptedKey)}.{Base64Url(iv)}.{Base64Url(ciphertext)}.{Base64Url(tag)}";
    }

    public string DecryptJwe(string jwe, RSA recipientPrivateKey)
    {
        var parts = jwe.Split('.');
        if (parts.Length != 5) throw new FormatException("JWEِ compact باید دقیقاً پنج بخش داشته باشد.");

        var aad = Encoding.ASCII.GetBytes(parts[0]);
        var encryptedKey = Base64UrlDecode(parts[1]);
        var iv = Base64UrlDecode(parts[2]);
        var ciphertext = Base64UrlDecode(parts[3]);
        var tag = Base64UrlDecode(parts[4]);

        var cek = recipientPrivateKey.Decrypt(encryptedKey, RSAEncryptionPadding.OaepSHA256);
        var plaintext = new byte[ciphertext.Length];
        using (var aesGcm = new AesGcm(cek, tag.Length))
            aesGcm.Decrypt(iv, ciphertext, tag, plaintext, aad);

        return Encoding.UTF8.GetString(plaintext);
    }

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string s)
    {
        var padded = s.Replace('-', '+').Replace('_', '/');
        padded += (padded.Length % 4) switch { 2 => "==", 3 => "=", _ => "" };
        return Convert.FromBase64String(padded);
    }
}
