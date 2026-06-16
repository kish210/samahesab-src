using SamaHesab.Application.Common.Validation;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>فاز RC (RC-7) — اعتبارسنجیِ کدِ ملی/شناسهٔ مالیاتی.</summary>
public class IranianIdentityTests
{
    [Theory]
    [InlineData("0499370899")]   // کدِ ملیِ معتبرِ نمونه (checksum صحیح)
    [InlineData("0084575948")]
    public void Valid_National_Codes_Pass(string code) => Assert.True(IranianIdentity.IsValidNationalCode(code));

    [Theory]
    [InlineData("1234567890")]   // checksumِ غلط
    [InlineData("0000000000")]   // همه‌یکسان
    [InlineData("12345")]        // طولِ غلط
    [InlineData("049937089X")]   // نویسهٔ نامعتبر → بعد از نرمال‌سازی ۹ رقم
    public void Invalid_National_Codes_Fail(string code) => Assert.False(IranianIdentity.IsValidNationalCode(code));

    [Fact]
    public void Empty_Is_Treated_Valid_Optional()
    {
        Assert.True(IranianIdentity.IsValidNationalCode(""));
        Assert.True(IranianIdentity.IsValidNationalCode(null));
        Assert.True(IranianIdentity.IsValidEconomicId(" "));
    }

    [Fact]
    public void Persian_Digits_Are_Normalized()
        => Assert.True(IranianIdentity.IsValidNationalCode("۰۴۹۹۳۷۰۸۹۹"));

    [Theory]
    [InlineData("10101010101")]   // ۱۱ رقم
    [InlineData("411111111111")]  // ۱۲ رقم
    [InlineData("14001234567890")]// ۱۴ رقم
    public void Economic_Id_Format_Pass(string v) => Assert.True(IranianIdentity.IsValidEconomicId(v));

    [Theory]
    [InlineData("123")]
    [InlineData("123456789")]     // ۹ رقم
    public void Economic_Id_Bad_Length_Fail(string v) => Assert.False(IranianIdentity.IsValidEconomicId(v));
}
