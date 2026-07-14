using System.Security.Cryptography;
using SamaHesab.Modules.TaxInvoicing.Crypto;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>U-ACCT-2 (سامانهٔ مودیان) — تستِ round-trip با کلیدهایِ آزمایشیِ خودمان (بدونِ اعتبارنامهٔ
/// واقعی/سرورِ Sandbox، طبقِ محدودیتِ مستندشده در todo.rm). این‌ها فقط درستیِ ریاضیِ RS256/RSA-OAEP-256/
/// A256GCM و سریال‌سازیِ compact را می‌سنجند، نه پذیرشِ واقعیِ سازمانِ مالیاتی.</summary>
public class ModianCryptoServiceTests
{
    private readonly IModianCryptoService _svc = new ModianCryptoService();

    [Fact]
    public void Jws_RoundTrip_Verifies_And_Returns_Original_Payload()
    {
        using var key = RSA.Create(2048);
        const string payload = "{\"invoiceId\":123,\"amount\":50000}";

        var jws = _svc.CreateJws(payload, key);
        var parts = jws.Split('.');

        Assert.Equal(3, parts.Length);
        Assert.Equal(payload, _svc.VerifyAndExtractJwsPayload(jws, key));
    }

    [Fact]
    public void Jws_Verification_Fails_With_Wrong_Key()
    {
        using var signingKey = RSA.Create(2048);
        using var otherKey = RSA.Create(2048);
        var jws = _svc.CreateJws("{\"x\":1}", signingKey);

        Assert.Throws<CryptographicException>(() => _svc.VerifyAndExtractJwsPayload(jws, otherKey));
    }

    [Fact]
    public void Jws_Verification_Fails_When_Payload_Is_Tampered()
    {
        using var key = RSA.Create(2048);
        var jws = _svc.CreateJws("{\"amount\":1000}", key);
        var parts = jws.Split('.');
        // دستکاریِ بدنه بدونِ دوباره‌امضاکردن — باید verify رد کند.
        var tampered = $"{parts[0]}.{parts[1]}AAAA.{parts[2]}";

        Assert.Throws<CryptographicException>(() => _svc.VerifyAndExtractJwsPayload(tampered, key));
    }

    [Fact]
    public void Jws_Includes_ExtraHeaderClaims()
    {
        using var key = RSA.Create(2048);
        var jws = _svc.CreateJws("{}", key, new Dictionary<string, object?> { ["sigT"] = "2026-07-14T10:00:00Z" });
        var headerJson = System.Text.Encoding.UTF8.GetString(
            Convert.FromBase64String(PadBase64Url(jws.Split('.')[0])));

        Assert.Contains("sigT", headerJson);
        Assert.Contains("RS256", headerJson);
    }

    [Fact]
    public void Jwe_RoundTrip_Decrypts_To_Original_Payload()
    {
        using var key = RSA.Create(2048);
        const string payload = "{\"uid\":\"1234567890123456789012\"}";

        var jwe = _svc.CreateJwe(payload, key);
        var parts = jwe.Split('.');

        Assert.Equal(5, parts.Length);
        Assert.Equal(payload, _svc.DecryptJwe(jwe, key));
    }

    [Fact]
    public void Jwe_Decryption_Fails_With_Wrong_Key()
    {
        using var recipientKey = RSA.Create(2048);
        using var otherKey = RSA.Create(2048);
        var jwe = _svc.CreateJwe("{\"x\":1}", recipientKey);

        Assert.ThrowsAny<CryptographicException>(() => _svc.DecryptJwe(jwe, otherKey));
    }

    [Fact]
    public void Jwe_Decryption_Fails_When_Ciphertext_Is_Tampered()
    {
        using var key = RSA.Create(2048);
        var jwe = _svc.CreateJwe("{\"amount\":1000}", key);
        var parts = jwe.Split('.');
        var tampered = $"{parts[0]}.{parts[1]}.{parts[2]}.{parts[3]}AAAA.{parts[4]}";

        Assert.ThrowsAny<CryptographicException>(() => _svc.DecryptJwe(tampered, key));
    }

    [Fact]
    public void Sign_Then_Encrypt_Full_RoundTrip_Like_Real_Submission_Flow()
    {
        // جریانِ واقعی: فاکتور را با کلیدِ خصوصیِ ما امضا (JWS) → کلِ JWS را با کلیدِ عمومیِ سرور رمزنگاری (JWE).
        using var senderSigningKey = RSA.Create(2048);
        using var serverKey = RSA.Create(2048);   // شبیه‌سازیِ کلیدِ عمومیِ سرورِ سامانهٔ مودیان
        const string invoiceJson = "{\"invoiceId\":123,\"totalAmount\":1500000}";

        var jws = _svc.CreateJws(invoiceJson, senderSigningKey);
        var jwe = _svc.CreateJwe(jws, serverKey);

        var decrypted = _svc.DecryptJwe(jwe, serverKey);
        Assert.Equal(jws, decrypted);
        Assert.Equal(invoiceJson, _svc.VerifyAndExtractJwsPayload(decrypted, senderSigningKey));
    }

    private static string PadBase64Url(string s)
    {
        var padded = s.Replace('-', '+').Replace('_', '/');
        padded += (padded.Length % 4) switch { 2 => "==", 3 => "=", _ => "" };
        return padded;
    }
}
