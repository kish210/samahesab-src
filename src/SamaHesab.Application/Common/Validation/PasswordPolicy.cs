using System.Linq;

namespace SamaHesab.Application.Common.Validation;

/// <summary>
/// فاز RC (RC-2) — سیاستِ رمزِ عبورِ تجاری: حداقل ۸ نویسه + دستِ‌کم یک حرف و یک رقم.
/// در هر مسیرِ تنظیم/تغییرِ رمز اعمال می‌شود.
/// </summary>
public static class PasswordPolicy
{
    public const int MinLength = 8;

    /// <summary>(ok, پیامِ خطا). معتبر → (true, null).</summary>
    public static (bool Ok, string? Error) Validate(string? password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < MinLength)
            return (false, $"رمزِ عبور باید حداقل {MinLength} نویسه باشد.");
        if (!password.Any(char.IsLetter) || !password.Any(char.IsDigit))
            return (false, "رمزِ عبور باید شاملِ حداقل یک حرف و یک رقم باشد.");
        return (true, null);
    }

    public static bool IsValid(string? password) => Validate(password).Ok;
}
