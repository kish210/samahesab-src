using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SamaHesab.WPF.Views.Shell;

/// <summary>
/// CC-4 (UX_ROADMAP) — راهنمای میان‌برِ صفحه‌کلید (F1). استانداردِ واحدِ میان‌برها یک‌جا اینجا
/// مستند و کشف‌پذیر می‌شود تا همهٔ فرم‌ها از آن پیروی کنند.
/// </summary>
public partial class ShortcutHelpWindow : Window
{
    // استانداردِ واحد — منبعِ یگانهٔ حقیقت برای میان‌برها.
    private static readonly (string Section, (string Key, string Action)[] Items)[] Map =
    {
        ("سراسری (پوسته)", new[]
        {
            ("Ctrl + K", "جست‌وجوی سراسری / پنجرهٔ دستورات"),
            ("Ctrl + ۱..۶", "میز کار / فروش / خرید / انبار / خزانه / اشخاص"),
            ("Ctrl + R", "گزارش‌ها"),
            ("F12", "صندوقِ فروش"),
            ("F1", "همین راهنما"),
        }),
        ("فرم‌ها (استانداردِ واحد)", new[]
        {
            ("F2", "جدید"),
            ("F3", "جست‌وجو"),
            ("F5", "بازخوانی"),
            ("F9", "ثبت / قطعی‌سازی"),
            ("Ctrl + S", "ذخیره"),
            ("Ctrl + P", "چاپ"),
            ("Esc", "انصراف / بستن"),
        }),
        ("ثبتِ سند", new[]
        {
            ("=", "توازنِ خودکار (پر کردنِ سمتِ خالی)"),
            ("Ctrl + Shift + V", "ورودِ انبوه از اکسل"),
            ("Enter", "افزودنِ ردیف"),
        }),
        ("گریدها / فهرست‌ها", new[]
        {
            ("Enter", "اقدامِ اصلی روی ردیف (ویرایش/وصول/تأیید…)"),
            ("Del", "حذف / برگشت"),
            ("↑ / ↓", "حرکت بین ردیف‌ها"),
        }),
    };

    public ShortcutHelpWindow()
    {
        InitializeComponent();
        foreach (var (section, items) in Map)
            Sections.Children.Add(BuildSection(section, items));
    }

    private FrameworkElement BuildSection(string title, (string Key, string Action)[] items)
    {
        var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 14) };
        panel.Children.Add(new TextBlock
        {
            Text = title, FontWeight = FontWeights.Bold, FontSize = 12.5, Margin = new Thickness(0, 0, 0, 6),
            Foreground = Brush("PrimaryText"), Opacity = 0.85
        });

        foreach (var (key, action) in items)
        {
            var grid = new Grid { Margin = new Thickness(0, 2, 0, 2) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var chip = new Border
            {
                Background = Brush("InputBackground"), BorderBrush = Brush("InputBorder"), BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4), Padding = new Thickness(8, 2, 8, 2),
                HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 0, 10, 0),
                Child = new TextBlock { Text = key, FontSize = 11.5, Foreground = Brush("PrimaryText"), FlowDirection = FlowDirection.LeftToRight }
            };
            Grid.SetColumn(chip, 0);
            grid.Children.Add(chip);

            var act = new TextBlock
            {
                Text = action, FontSize = 12.5, VerticalAlignment = VerticalAlignment.Center,
                Foreground = Brush("PrimaryText"), TextWrapping = TextWrapping.Wrap
            };
            Grid.SetColumn(act, 1);
            grid.Children.Add(act);

            panel.Children.Add(grid);
        }
        return panel;
    }

    private Brush Brush(string key)
        => TryFindResource(key) as Brush ?? Brushes.Gray;

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape || e.Key == Key.F1) { Close(); e.Handled = true; }
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
