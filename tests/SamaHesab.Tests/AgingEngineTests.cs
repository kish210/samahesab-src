using SamaHesab.Application.Reports;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>فاز ۱۲ (RC) — موتورِ ماندهٔ سنی‌شده.</summary>
public class AgingEngineTests
{
    [Theory]
    [InlineData(-5, 0)]   // سررسیدنشده → جاری
    [InlineData(0, 0)]
    [InlineData(30, 0)]
    [InlineData(31, 1)]
    [InlineData(60, 1)]
    [InlineData(61, 2)]
    [InlineData(90, 2)]
    [InlineData(91, 3)]
    [InlineData(400, 3)]
    public void BucketOf_boundaries(int ageDays, int expected)
        => Assert.Equal(expected, AgingEngine.BucketOf(ageDays));

    [Fact]
    public void Accumulates_into_correct_buckets()
    {
        var b = new AgingBuckets();
        b.Add(100, 10);    // جاری
        b.Add(200, 45);    // ۳۱–۶۰
        b.Add(50, 75);     // ۶۱–۹۰
        b.Add(300, 120);   // بیش از ۹۰

        Assert.Equal(100, b.Current);
        Assert.Equal(200, b.D31_60);
        Assert.Equal(50, b.D61_90);
        Assert.Equal(300, b.Over90);
        Assert.Equal(650, b.Total);
    }
}
