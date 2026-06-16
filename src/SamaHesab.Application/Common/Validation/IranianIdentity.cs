using System.Text;

namespace SamaHesab.Application.Common.Validation;

/// <summary>
/// فاز RC (RC-7) — اعتبارسنجیِ هویتِ مالیاتیِ ایران برای فاکتورِ رسمی.
/// کدِ ملیِ حقیقی (۱۰ رقم) با الگوریتمِ checksumِ استاندارد بررسی می‌شود؛ شناسهٔ حقوقی/کدِ اقتصادی
/// فقط فرمت/طول (تا ورودیِ معتبر به‌خاطرِ الگوریتمِ مورد‌اختلاف اشتباه رد نشود). مقدارِ خالی «معتبر»
/// در نظر گرفته می‌شود (فیلد اختیاری).
/// </summary>
public static class IranianIdentity
{
    /// <summary>ارقامِ فارسی/عربی → ASCII و حذفِ نویسه‌های غیرعددی.</summary>
    public static string NormalizeDigits(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return string.Empty;
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s.Trim())
        {
            if (ch >= '۰' && ch <= '۹') sb.Append((char)('0' + (ch - '۰')));
            else if (ch >= '٠' && ch <= '٩') sb.Append((char)('0' + (ch - '٠')));
            else if (char.IsDigit(ch)) sb.Append(ch);
        }
        return sb.ToString();
    }

    /// <summary>کدِ ملیِ حقیقیِ ۱۰رقمی (با checksum). خالی → true (اختیاری).</summary>
    public static bool IsValidNationalCode(string? value)
    {
        var d = NormalizeDigits(value);
        if (d.Length == 0) return true;
        if (d.Length != 10) return false;
        if (new string(d[0], 10) == d) return false;   // همه‌رقمِ یکسان نامعتبر

        var sum = 0;
        for (var i = 0; i < 9; i++) sum += (d[i] - '0') * (10 - i);
        var r = sum % 11;
        var check = d[9] - '0';
        return r < 2 ? check == r : check == 11 - r;
    }

    /// <summary>شناسهٔ ملیِ حقوقی یا کدِ اقتصادی — فقط فرمت (ارقام + طولِ متعارف ۱۱/۱۲/۱۴). خالی → true.</summary>
    public static bool IsValidEconomicId(string? value)
    {
        var d = NormalizeDigits(value);
        if (d.Length == 0) return true;
        return d.Length is 11 or 12 or 14;
    }
}
