using System.Windows;
using System.Windows.Controls;

namespace SamaHesab.WPF.Views.POS;

public partial class RestaurantPosView : UserControl
{
    public RestaurantPosView() => InitializeComponent();

    private void ServerSettings_Click(object sender, RoutedEventArgs e)
    {
        new Shell.ConnectionSettingsWindow { Owner = Window.GetWindow(this) }.ShowDialog();
    }
}
