using System;
using SamaHesab.Application.Licensing;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>فاز ۱۲ P-G7 — هستهٔ لایسنس: تولید/اعتبارسنجیِ کلیدِ مقیّد به ماشین + تریال.</summary>
public class LicenseKeyTests
{
    private const string Secret = "test-vendor-secret";

    [Fact]
    public void Generated_Key_Validates_For_Same_Machine()
    {
        var key = LicenseKey.Generate("MACHINE-ABC", Secret);
        Assert.True(LicenseKey.Validate("MACHINE-ABC", key, Secret));
        Assert.Matches("^[A-Z2-9]{5}-[A-Z2-9]{5}-[A-Z2-9]{5}-[A-Z2-9]{5}$", key);
    }

    [Fact]
    public void Key_Is_Case_And_Whitespace_Insensitive()
    {
        var key = LicenseKey.Generate("Machine-ABC", Secret);
        Assert.True(LicenseKey.Validate("machine-abc", "  " + key.ToLowerInvariant() + " ", Secret));
    }

    [Fact]
    public void Key_Fails_For_Different_Machine()
    {
        var key = LicenseKey.Generate("MACHINE-ABC", Secret);
        Assert.False(LicenseKey.Validate("MACHINE-XYZ", key, Secret));
    }

    [Fact]
    public void Key_Fails_With_Wrong_Secret()
    {
        var key = LicenseKey.Generate("MACHINE-ABC", Secret);
        Assert.False(LicenseKey.Validate("MACHINE-ABC", key, "other-secret"));
    }

    [Fact]
    public void Trial_Counts_Down_And_Expires()
    {
        var install = new DateTime(2026, 1, 1);
        Assert.Equal(30, TrialPolicy.DaysRemaining(install, install));
        Assert.Equal(20, TrialPolicy.DaysRemaining(install, install.AddDays(10)));
        Assert.True(TrialPolicy.Expired(install, install.AddDays(30)));
        Assert.False(TrialPolicy.Expired(install, install.AddDays(29)));
    }
}
