namespace SamaHesab.Application.Common.Barcode;

/// <summary>
/// کار #۲۷ — منطق خالصِ بارکد، مشترک بین POS/رسید/حواله/انبارگردانی.
/// نرمال‌سازی ارقام فارسی/عربی + تشخیص بارکدهای وزنی/قیمت‌دار (EAN-13 با پیشوند ۲).
/// </summary>
public static class BarcodeParser
{
    /// <summary>ارقام فارسی (۰-۹) و عربی (٠-٩) را به لاتین تبدیل و فاصله‌ها را حذف می‌کند.</summary>
    public static string Normalize(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return string.Empty;
        var arr = code.Trim().ToCharArray();
        for (int i = 0; i < arr.Length; i++)
        {
            char c = arr[i];
            if (c >= '۰' && c <= '۹') arr[i] = (char)('0' + (c - '۰')); // فارسی
            else if (c >= '٠' && c <= '٩') arr[i] = (char)('0' + (c - '٠')); // عربی
        }
        return new string(arr).Replace(" ", string.Empty);
    }

    public static bool IsAllDigits(string s)
    {
        if (s.Length == 0) return false;
        foreach (var c in s) if (c < '0' || c > '9') return false;
        return true;
    }

    /// <summary>
    /// بارکد وزنی/قیمت‌دارِ فروشگاهی (EAN-13، پیشوند «۲»): «2 IIIIII VVVVV C».
    /// رقم اول=۲، شش رقمِ بعد=کد کالا، پنج رقمِ بعد=مقدار جاسازی‌شده (قیمت/وزن)، رقم آخر=کنترل.
    /// در صورت موفقیت: کد کالا (بدون صفرهای ابتدایی) و مقدار جاسازی‌شده برمی‌گردد.
    /// </summary>
    public static bool TryParseWeighted(string normalized, out string itemCode, out decimal embeddedValue)
    {
        itemCode = string.Empty; embeddedValue = 0;
        if (normalized.Length != 13 || normalized[0] != '2' || !IsAllDigits(normalized)) return false;
        itemCode = normalized.Substring(1, 6).TrimStart('0');
        if (itemCode.Length == 0) itemCode = "0";
        embeddedValue = decimal.Parse(normalized.Substring(7, 5));
        return true;
    }
}
