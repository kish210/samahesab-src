using System.Windows;
using System.Windows.Controls;

namespace SamaHesab.WPF.Views.POS;

public partial class PosView : UserControl
{
    public PosView() => InitializeComponent();

    private void ServerSettings_Click(object sender, RoutedEventArgs e)
    {
        new Shell.ConnectionSettingsWindow { Owner = Window.GetWindow(this) }.ShowDialog();
    }
}
