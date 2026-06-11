using SamaHesab.Application.Common.Barcode;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>کار #۲۷ — منطق خالصِ بارکد یکپارچه.</summary>
public class BarcodeParserTests
{
    [Theory]
    [InlineData("۱۲۳۴۵", "12345")]      // فارسی
    [InlineData("٤٥٦", "456")]          // عربی
    [InlineData("  6011 234  ", "6011234")] // trim + حذف فاصله
    [InlineData("", "")]
    public void Normalize_Converts_And_Trims(string input, string expected)
        => Assert.Equal(expected, BarcodeParser.Normalize(input));

    [Fact]
    public void IsAllDigits_Works()
    {
        Assert.True(BarcodeParser.IsAllDigits("12345"));
        Assert.False(BarcodeParser.IsAllDigits("12a45"));
        Assert.False(BarcodeParser.IsAllDigits(""));
    }

    [Fact]
    public void TryParseWeighted_Extracts_ItemCode_And_Value()
    {
        // 2 | 001234 | 04500 | C   → کد کالا 1234، مقدار جاسازی 4500
        var ok = BarcodeParser.TryParseWeighted("2001234045007", out var itemCode, out var value);
        Assert.True(ok);
        Assert.Equal("1234", itemCode);
        Assert.Equal(4500m, value);
    }

    [Theory]
    [InlineData("1234567890123")]  // پیشوند ۲ نیست
    [InlineData("200123404500")]   // ۱۲ رقم (نه ۱۳)
    [InlineData("20012340450AB")]  // غیرعددی
    public void TryParseWeighted_Rejects_NonWeighted(string code)
        => Assert.False(BarcodeParser.TryParseWeighted(code, out _, out _));
}
