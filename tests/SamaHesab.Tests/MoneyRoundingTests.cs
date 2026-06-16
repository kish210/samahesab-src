using SamaHesab.Application.Common;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>🇮🇷 POS-IR-1 — گرد کردنِ مبلغ به نزدیک‌ترین پله.</summary>
public class MoneyRoundingTests
{
    [Theory]
    [InlineData(12340, 1000, 12000)]
    [InlineData(12600, 1000, 13000)]
    [InlineData(12500, 1000, 13000)]   // نقطهٔ میانی → بالا
    [InlineData(12340, 5000, 10000)]
    [InlineData(13000, 5000, 15000)]
    [InlineData(99999, 0, 99999)]      // بدونِ گرد کردن
    [InlineData(99999, -1, 99999)]
    public void RoundTo_Works(decimal amount, int step, decimal expected)
        => Assert.Equal(expected, MoneyRounding.RoundTo(amount, step));

    [Fact]
    public void Adjustment_Is_Difference()
        => Assert.Equal(-340m, MoneyRounding.Adjustment(12340, 1000));   // ۱۲۰۰۰ − ۱۲۳۴۰
}
