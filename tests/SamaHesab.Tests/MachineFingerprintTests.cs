using SamaHesab.Infrastructure.Services.Licensing;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>فاز ۱۲ P-G7 — اثرِانگشتِ سخت‌افزاریِ واقعی (WMI مادربرد/CPU + رجیستری MachineGuid).</summary>
public class MachineFingerprintTests
{
    [Fact]
    public void Fingerprint_Is_Stable_32_Hex()
    {
        var sut = new MachineFingerprintProvider();
        var a = sut.GetFingerprint();
        var b = sut.GetFingerprint();

        Assert.Equal(32, a.Length);
        Assert.Matches("^[0-9A-F]{32}$", a);
        Assert.Equal(a, b);   // پایدار بینِ دو فراخوان
    }
}
