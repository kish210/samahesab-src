using System.Windows;
using System.Windows.Controls;

namespace SamaHesab.WPF.Views.POS;

public partial class PosView : UserControl
{
    public PosView()
    {
        InitializeComponent();
        // OPT-11: فوکوسِ همیشگیِ نوارِ بارکد برای اسکنِ پیوسته و سریع
        Loaded += (_, _) => BarcodeBox.Focus();
    }

    private void ServerSettings_Click(object sender, RoutedEventArgs e)
    {
        new Shell.ConnectionSettingsWindow { Owner = Window.GetWindow(this) }.ShowDialog();
    }
}
