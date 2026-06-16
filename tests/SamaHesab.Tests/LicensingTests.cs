using System;
using System.Security.Cryptography;
using SamaHesab.Application.Licensing;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>فاز ۱۲ P-G7 — هستهٔ لایسنس: امضای RSA، تشخیصِ دستکاری، قفلِ ماشین، انقضا، تریال، رده‌ها.</summary>
public class LicensingTests
{
    private static (string pub, string priv) NewKeys()
    {
        using var rsa = RSA.Create(2048);
        return (rsa.ExportSubjectPublicKeyInfoPem(), rsa.ExportPkcs8PrivateKeyPem());
    }

    private static LicenseInfo Sample(string fp = "FP-ABC", LicenseTier tier = LicenseTier.Professional,
        int days = 365)
    {
        var (mb, mu) = LicenseLimits.For(tier);
        var now = DateTime.UtcNow;
        return new LicenseInfo("شرکتِ نمونه", "10101", fp, tier, now, now.AddDays(days), mb, mu);
    }

    [Fact]
    public void Sign_Then_Verify_Succeeds()
    {
        var (pub, priv) = NewKeys();
        var info = Sample();
        var doc = new LicenseDocument(info, RsaLicense.Sign(info, priv));
        Assert.True(RsaLicense.Verify(doc, pub));
    }

    [Fact]
    public void Tampered_Payload_Fails_Verification()
    {
        var (pub, priv) = NewKeys();
        var info = Sample();
        var sig = RsaLicense.Sign(info, priv);
        // دستکاری: ارتقای رده/سقفِ کاربر بدونِ امضای تازه
        var tampered = new LicenseDocument(info with { Tier = LicenseTier.Enterprise, MaxUsers = 9999 }, sig);
        Assert.False(RsaLicense.Verify(tampered, pub));
    }

    [Fact]
    public void Verify_Fails_With_Foreign_PublicKey()
    {
        var (_, priv) = NewKeys();
        var (otherPub, _) = NewKeys();
        var info = Sample();
        var doc = new LicenseDocument(info, RsaLicense.Sign(info, priv));
        Assert.False(RsaLicense.Verify(doc, otherPub));
    }

    [Fact]
    public void Validator_Valid_License()
    {
        var (pub, priv) = NewKeys();
        var info = Sample(fp: "MACHINE-1");
        var doc = new LicenseDocument(info, RsaLicense.Sign(info, priv));
        var res = new LicenseValidator(pub).Validate(doc, "MACHINE-1", DateTime.UtcNow);
        Assert.Equal(LicenseStatus.Valid, res.Status);
        Assert.True(res.IsValid);
    }

    [Fact]
    public void Validator_Wrong_Machine()
    {
        var (pub, priv) = NewKeys();
        var info = Sample(fp: "MACHINE-1");
        var doc = new LicenseDocument(info, RsaLicense.Sign(info, priv));
        var res = new LicenseValidator(pub).Validate(doc, "MACHINE-2", DateTime.UtcNow);
        Assert.Equal(LicenseStatus.WrongMachine, res.Status);
    }

    [Fact]
    public void Validator_Expired()
    {
        var (pub, priv) = NewKeys();
        var info = Sample(fp: "M", days: 10);
        var doc = new LicenseDocument(info, RsaLicense.Sign(info, priv));
        var res = new LicenseValidator(pub).Validate(doc, "M", DateTime.UtcNow.AddDays(11));
        Assert.Equal(LicenseStatus.Expired, res.Status);
    }

    [Fact]
    public void Validator_None_And_BadSignature()
    {
        var (pub, _) = NewKeys();
        var v = new LicenseValidator(pub);
        Assert.Equal(LicenseStatus.None, v.Validate(null, "M", DateTime.UtcNow).Status);

        var info = Sample();
        var bad = new LicenseDocument(info, "bm90LXZhbGlk"); // امضای جعلی
        Assert.Equal(LicenseStatus.BadSignature, v.Validate(bad, info.MachineFingerprint, DateTime.UtcNow).Status);
    }

    [Fact]
    public void Document_Json_RoundTrips()
    {
        var (_, priv) = NewKeys();
        var info = Sample();
        var doc = new LicenseDocument(info, RsaLicense.Sign(info, priv));
        var back = LicenseDocument.FromJson(doc.ToJson());
        Assert.NotNull(back);
        Assert.Equal(doc.Signature, back!.Signature);
        Assert.Equal(info.CompanyName, back.License.CompanyName);
        Assert.Equal(info.ExpiresUtc, back.License.ExpiresUtc, TimeSpan.FromSeconds(1));
    }

    [Theory]
    [InlineData(LicenseTier.Starter, 1, 3)]
    [InlineData(LicenseTier.Professional, 3, 10)]
    public void Tier_Limits(LicenseTier tier, int branches, int users)
    {
        var (b, u) = LicenseLimits.For(tier);
        Assert.Equal(branches, b);
        Assert.Equal(users, u);
    }

    [Fact]
    public void Enterprise_Is_Unlimited()
    {
        var (b, u) = LicenseLimits.For(LicenseTier.Enterprise);
        Assert.True(LicenseLimits.IsUnlimited(b));
        Assert.True(LicenseLimits.IsUnlimited(u));
    }

    [Fact]
    public void Trial_Expires_By_Days_Or_Vouchers()
    {
        var install = new DateTime(2026, 1, 1);
        // فعال در ابتدا
        var a = TrialPolicy.Evaluate(install, install.AddDays(10), voucherCount: 50);
        Assert.Equal(TrialState.Active, a.State);
        Assert.Equal(110, a.DaysRemaining);
        Assert.Equal(150, a.VouchersRemaining);
        // انقضا با روز
        Assert.Equal(TrialState.Expired, TrialPolicy.Evaluate(install, install.AddDays(120), 0).State);
        // انقضا با تعداد سند
        Assert.Equal(TrialState.Expired, TrialPolicy.Evaluate(install, install.AddDays(1), 200).State);
    }
}
