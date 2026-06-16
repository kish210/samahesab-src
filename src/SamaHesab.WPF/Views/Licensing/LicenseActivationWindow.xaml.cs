using System.Windows;
using SamaHesab.WPF.ViewModels.Licensing;

namespace SamaHesab.WPF.Views.Licensing;

/// <summary>فاز ۱۲ P-G7 — پنجرهٔ فعال‌سازی/تریال. انتخابِ فایل در code-behind؛ منطق در VM.</summary>
public partial class LicenseActivationWindow : Window
{
    private readonly LicenseActivationViewModel _vm;

    public LicenseActivationWindow(LicenseActivationViewModel vm)
    {
        InitializeComponent();
        DataContext = _vm = vm;
        _vm.Finished += () => Dispatcher.Invoke(Close);
    }

    private async void LoadLicense_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "انتخابِ فایلِ لایسنس",
            Filter = "فایلِ لایسنس (*.lic)|*.lic|همه فایل‌ها (*.*)|*.*",
        };
        if (dlg.ShowDialog() == true)
            await _vm.InstallFromFileAsync(dlg.FileName);
    }
}
