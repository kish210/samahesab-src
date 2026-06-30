using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SamaHesab.WPF.Services;

/// <summary>
/// L2 — تولیدِ بارکدِ Code128-B به‌صورتِ برداری (بدونِ کتابخانهٔ خارجی) برای پیش‌نمایش/چاپ.
/// همچنین ساختِ «برچسبِ» کاملِ کالا (نام + بارکد + کد + قیمت) برای چاپِ تکی/تعدادی.
/// </summary>
public static class BarcodeService
{
    // الگوی ۱۰۷ نمادِ Code128 — هر نماد عرضِ ۶ میله/فاصله (نمادِ پایان ۷ تایی است).
    private static readonly string[] P =
    {
        "212222","222122","222221","121223","121322","131222","122213","122312","132212","221213",
        "221312","231212","112232","122132","122231","113222","123122","123221","223211","221132",
        "221231","213212","223112","312131","311222","321122","321221","312212","322112","322211",
        "212123","212321","232121","111323","131123","131321","112313","132113","132311","211313",
        "231113","231311","112133","112331","132131","113123","113321","133121","313121","211331",
        "231131","213113","213311","213131","311123","311321","331121","312113","312311","332111",
        "314111","221411","431111","111224","111422","121124","121421","141122","141221","112214",
        "112412","122114","122411","142112","142211","241211","221114","413111","241112","134111",
        "111242","121142","121241","114212","124112","124211","411212","421112","421211","212141",
        "214121","412121","111143","111341","131141","114113","114311","411113","411311","113141",
        "114131","311141","411131","211412","211214","211232","2331112"
    };

    /// <summary>هندسهٔ میله‌های Code128-B برای رشتهٔ ASCII (۳۲..۱۲۶). module = عرضِ هر ماژول (px).</summary>
    public static DrawingImage Code128Image(string data, double module = 2, double height = 52)
    {
        data ??= "";
        var codes = new System.Collections.Generic.List<int> { 104 }; // Start-B
        long sum = 104;
        for (int i = 0; i < data.Length; i++)
        {
            int v = data[i] - 32;
            if (v < 0 || v > 94) v = 0;            // خارج از Code128-B → فاصله
            codes.Add(v);
            sum += (long)v * (i + 1);
        }
        codes.Add((int)(sum % 103));               // checksum
        codes.Add(106);                            // Stop

        var geo = new GeometryGroup();
        double x = 0;
        foreach (var c in codes)
        {
            var pat = P[c];
            bool bar = true;                       // هر نماد با میله شروع می‌شود
            foreach (var ch in pat)
            {
                double w = (ch - '0') * module;
                if (bar) geo.Children.Add(new RectangleGeometry(new Rect(x, 0, w, height)));
                x += w; bar = !bar;
            }
        }
        var dg = new GeometryDrawing(Brushes.Black, null, geo);
        var img = new DrawingImage(dg);
        img.Freeze();
        return img;
    }

    /// <summary>برچسبِ کاملِ کالا: نام (اختیاری) + بارکدِ برداری + کدِ خوانا + قیمت (اختیاری).</summary>
    public static FrameworkElement BuildLabel(string name, string code, string? priceText,
        bool showName, bool showPrice, double widthPx = 189, double heightPx = 113)
    {
        var panel = new StackPanel
        {
            Width = widthPx, Height = heightPx, Margin = new Thickness(3),
            Background = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center
        };
        var border = new Border { BorderBrush = Brushes.LightGray, BorderThickness = new Thickness(0.5), Child = panel };

        if (showName && !string.IsNullOrWhiteSpace(name))
            panel.Children.Add(new TextBlock
            {
                Text = name, FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = Brushes.Black,
                TextAlignment = TextAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis,
                Margin = new Thickness(2, 4, 2, 2), HorizontalAlignment = HorizontalAlignment.Center
            });

        panel.Children.Add(new Image
        {
            Source = Code128Image(string.IsNullOrWhiteSpace(code) ? " " : code, 1.6, 46),
            Stretch = Stretch.Fill, Height = 46, Width = widthPx - 18,
            Margin = new Thickness(0, 2, 0, 0), HorizontalAlignment = HorizontalAlignment.Center,
            SnapsToDevicePixels = true
        });
        panel.Children.Add(new TextBlock
        {
            Text = code, FontSize = 10, FontFamily = new FontFamily("Consolas"), Foreground = Brushes.Black,
            TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 1, 0, 0)
        });
        if (showPrice && !string.IsNullOrWhiteSpace(priceText))
            panel.Children.Add(new TextBlock
            {
                Text = priceText, FontSize = 11, FontWeight = FontWeights.Bold, Foreground = Brushes.Black,
                TextAlignment = TextAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center,
                FlowDirection = FlowDirection.RightToLeft, Margin = new Thickness(0, 1, 0, 0)
            });
        return border;
    }
}
