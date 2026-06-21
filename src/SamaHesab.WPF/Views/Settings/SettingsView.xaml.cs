using System.Windows;
using System.Windows.Controls;

namespace SamaHesab.WPF.Views.Settings;

public partial class SettingsView : UserControl
{
    public SettingsView() => InitializeComponent();

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
