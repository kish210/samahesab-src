using System.Security.Cryptography;

namespace SamaHesab.WPF.Services;

/// <summary>U-SEC-RECOVERY — تولیدِ کدِ بازیابیِ رمز (فقط سمتِ کلاینت؛ کدِ خام هرگز به سرور/DB
/// نمی‌رود، فقط هشش از طریقِ SetRecoveryCodeCommand ذخیره می‌شود). حروف/ارقامِ مشابه‌الشکل
/// (0/O، 1/I/L) عمداً حذف شده‌اند تا در دستنویسی/چاپ اشتباه خوانده نشوند.</summary>
public static class RecoveryCodeGenerator
{
    private const string Alphabet = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    /// <summary>کدی مثلِ "XXXX-XXXX-XXXX-XXXX" (۱۶ نویسهٔ معنادار + خط‌فاصله برایِ خوانایی).</summary>
    public static string Generate()
    {
        var chars = new char[16];
        var bytes = RandomNumberGenerator.GetBytes(16);
        for (int i = 0; i < 16; i++)
            chars[i] = Alphabet[bytes[i] % Alphabet.Length];
        var s = new string(chars);
        return string.Join("-", s.Chunk(4).Select(c => new string(c)));
    }
}
