using SamaHesab.Application.Common.Validation;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>فاز RC (RC-2) — سیاستِ رمزِ عبور (حداقل ۸ + حرف و رقم).</summary>
public class PasswordPolicyTests
{
    [Theory]
    [InlineData("admin123")]   // ۸ نویسه، حرف+رقم
    [InlineData("P@ssw0rd")]
    [InlineData("samahesab2026")]
    public void Strong_Passwords_Pass(string p) => Assert.True(PasswordPolicy.IsValid(p));

    [Theory]
    [InlineData("1234")]        // کوتاه
    [InlineData("abc12")]       // کوتاه
    [InlineData("12345678")]    // فقط رقم
    [InlineData("abcdefgh")]    // فقط حرف
    [InlineData("")]            // خالی
    [InlineData(null)]
    public void Weak_Passwords_Fail(string? p) => Assert.False(PasswordPolicy.IsValid(p));
}
