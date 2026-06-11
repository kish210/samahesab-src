using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SamaHesab.WPF.Converters;

/// <summary>Maps a money amount to a bar height in pixels. ConverterParameter = max amount (divisor).</summary>
public class AmountToBarHeightConverter : IValueConverter
{
    public static readonly AmountToBarHeightConverter Instance = new();
    private const double MaxPixels = 130;

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        double amount = value switch
        {
            decimal d => (double)d,
            double db => db,
            int i => i,
            _ => 0
        };
        double max = 1;
        if (parameter != null) double.TryParse(parameter.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out max);
        if (max <= 0) max = 1;
        var h = amount / max * MaxPixels;
        if (h < 2) h = 2;
        if (h > MaxPixels) h = MaxPixels;
        return h;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var boolVal = value is bool b && b;
        var invert = parameter?.ToString() == "Invert";
        return (boolVal ^ invert) ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Visibility v && v == Visibility.Visible;
}

public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b && !b;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is bool b && !b;
}

public class NumberFormatConverter : IValueConverter
{
    // ارقام فارسی + جداکننده‌ی هزارگان فارسی (٬) — طبق design-system
    private static readonly char[] FaDigits = { '۰', '۱', '۲', '۳', '۴', '۵', '۶', '۷', '۸', '۹' };

    public static string ToPersian(string s)
    {
        var arr = s.ToCharArray();
        for (int k = 0; k < arr.Length; k++)
        {
            if (arr[k] >= '0' && arr[k] <= '9') arr[k] = FaDigits[arr[k] - '0'];
            else if (arr[k] == ',') arr[k] = '٬';
        }
        return new string(arr);
    }

    private static string ToLatin(string s)
    {
        var arr = s.ToCharArray();
        for (int k = 0; k < arr.Length; k++)
        {
            int idx = System.Array.IndexOf(FaDigits, arr[k]);
            if (idx >= 0) arr[k] = (char)('0' + idx);
        }
        return new string(arr).Replace("٬", "").Replace(",", "");
    }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is decimal d) return ToPersian(d.ToString("N0"));
        if (value is double dbl) return ToPersian(dbl.ToString("N0"));
        if (value is int i) return ToPersian(i.ToString("N0"));
        return value?.ToString() ?? string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (decimal.TryParse(ToLatin(value?.ToString() ?? string.Empty), out var d)) return d;
        return 0m;
    }
}

public class CurrencyConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is decimal d) return $"{d:N0} ریال";
        return "0 ریال";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class GreaterThanZeroConverter : IValueConverter
{
    public static readonly GreaterThanZeroConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int i) return i > 0;
        if (value is decimal d) return d > 0;
        if (value is double dbl) return dbl > 0;
        return false;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class GreaterThanZeroVisibilityConverter : IValueConverter
{
    public static readonly GreaterThanZeroVisibilityConverter Instance = new();

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool result = false;
        if (value is int i) result = i > 0;
        else if (value is decimal d) result = d > 0;
        else if (value is double dbl) result = dbl > 0;
        return result ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class StatusToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value?.ToString() switch
        {
            "قطعی" or "فعال" or "موجود" => "#27AE60",
            "پیش‌نویس" or "در انتظار" => "#3498DB",
            "لغو شده" or "غیرفعال" => "#E74C3C",
            "در جریان" => "#F39C12",
            _ => "#8E9AAB"
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

public class NullToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var isNull = value == null;
        var invert = parameter?.ToString() == "Invert";
        return (isNull ^ invert) ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
