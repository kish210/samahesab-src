using System;
using System.Linq;
using System.Windows;

namespace SamaHesab.WPF.Services;

/// <summary>
/// مدیریتِ چگالیِ رابط (عادی/فشرده). دیکشنریِ توکن‌های چگالی را هنگامِ اجرا جابه‌جا می‌کند
/// تا سبک‌های دیزاین‌سیستم — که با <c>DynamicResource</c> به این توکن‌ها وصل‌اند — زنده به‌روز شوند.
/// عادی = مقادیرِ فعلی (تغییرِ ظاهری ندارد)؛ فشرده = ارتفاع/پدینگِ کمتر.
/// </summary>
public static class DensityManager
{
    // کلیدِ نشانه برای یافتنِ دیکشنریِ چگالیِ فعلی در میانِ MergedDictionaries.
    private const string Marker = "ErpDensRowHeight";

    public static bool IsCompact { get; private set; }

    public static void Apply(bool compact)
    {
        IsCompact = compact;
        var app = System.Windows.Application.Current;
        if (app is null) return;

        var dict = new ResourceDictionary
        {
            Source = new Uri(
                compact ? "Assets/Themes/DensityCompact.xaml" : "Assets/Themes/DensityNormal.xaml",
                UriKind.Relative)
        };

        var merged = app.Resources.MergedDictionaries;
        var existing = merged.FirstOrDefault(d => d.Contains(Marker));
        if (existing != null) merged.Remove(existing);
        merged.Add(dict);   // DynamicResource مستقل از ترتیب، کلید را دوباره resolve می‌کند.
    }
}
