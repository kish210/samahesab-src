using System.Linq;
using System.Reflection;

namespace SamaHesab.WPF.Services;

/// <summary>نمایشِ نسخهٔ برنامه از منبعِ واحد (اسمبلی = Directory.Build.props) با ارقامِ فارسی.</summary>
public static class AppVersion
{
    /// <summary>نسخهٔ سه‌بخشی، مثلِ «۲.۱.۰».</summary>
    public static string Display
    {
        get
        {
            var v = Assembly.GetExecutingAssembly().GetName().Version ?? new System.Version(0, 0, 0);
            return ToPersianDigits($"{v.Major}.{v.Minor}.{v.Build}");
        }
    }

    private static string ToPersianDigits(string s)
        => new string(s.Select(c => char.IsDigit(c) ? (char)('۰' + (c - '0')) : c).ToArray());
}
