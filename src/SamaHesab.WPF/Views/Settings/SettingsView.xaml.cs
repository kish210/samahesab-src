using System.Windows;
using System.Windows.Controls;

namespace SamaHesab.WPF.Views.Settings;

public partial class SettingsView : UserControl
{
    public SettingsView() => InitializeComponent();

    /// <summary>UX-SETTINGS-SEARCH — فیلترِ زندهٔ دکمه‌های بخش بر اساسِ متنِ جست‌وجو.</summary>
    private void SettingsSearch_Changed(object sender, TextChangedEventArgs e)
    {
        var q = (SettingsSearchBox.Text ?? string.Empty).Trim();
        bool searching = q.Length > 0;
        // سرتیترهای گروه فقط وقتی جست‌وجو نمی‌شود نمایش داده شوند.
        HdrPage.Visibility = HdrSpecial.Visibility = searching ? Visibility.Collapsed : Visibility.Visible;
        foreach (var child in SettingsNav.Children)
        {
            if (child is Button b && b.Content is string txt)
                b.Visibility = (!searching || txt.Contains(q, System.StringComparison.OrdinalIgnoreCase))
                    ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    // ناوبریِ سایدبار به بخش‌های همین صفحه: اسکرول به کارتِ مربوطه.
    private void Section_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button b || b.Tag is not string tag) return;
        FrameworkElement? target = tag switch
        {
            "Company" => CompanySection,
            "Appearance" => AppearanceSection,
            "Sms" => SmsSection,
            "Support" => SupportSection,
            "About" => AboutSection,
            _ => null
        };
        target?.BringIntoView();
    }
}
