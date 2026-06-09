using System.Windows;
using SamaHesab.WPF.ViewModels.Shell;

namespace SamaHesab.WPF.Views.Shell;

public partial class LoginWindow : Window
{
    private readonly LoginViewModel _vm;

    public LoginWindow(LoginViewModel viewModel)
    {
        InitializeComponent();
        _vm = viewModel;
        DataContext = viewModel;
        Resources["BoolVis"] = new System.Windows.Controls.BooleanToVisibilityConverter();
        Loaded += (_, _) =>
        {
            TxtUsername?.Focus();
            if (_vm.IsApiMode) BtnSettings.Content = "⚙  تنظیمات اتصال به سرور";
        };
    }

    private void PwdBox_PasswordChanged(object sender, RoutedEventArgs e) =>
        _vm.Password = PwdBox.Password;

    private void CloseButton_Click(object sender, RoutedEventArgs e) =>
        System.Windows.Application.Current.Shutdown();

    private void ConnectionSettings_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.IsApiMode)
        {
            // POS / restaurant clients connect to the central Web API server, not the DB.
            new ApiSettingsWindow { Owner = this }.ShowDialog();
            return;
        }
        var dlg = new ConnectionSettingsWindow { Owner = this };
        dlg.ShowDialog();
    }
}
