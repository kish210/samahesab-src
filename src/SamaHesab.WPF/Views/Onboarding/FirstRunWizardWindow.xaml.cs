using System.Windows;
using SamaHesab.WPF.ViewModels.Onboarding;

namespace SamaHesab.WPF.Views.Onboarding;

/// <summary>فاز ۱۲ G3 — ویزاردِ راه‌اندازیِ اولیه. PasswordBox به‌خاطرِ امنیت در code-behind به VM داده می‌شود.</summary>
public partial class FirstRunWizardWindow : Window
{
    private readonly FirstRunWizardViewModel _vm;

    public FirstRunWizardWindow(FirstRunWizardViewModel vm)
    {
        InitializeComponent();
        DataContext = _vm = vm;
        _vm.Finished += () => Dispatcher.Invoke(Close);
    }

    private void PwdNew_Changed(object sender, RoutedEventArgs e) => _vm.NewPassword = PwdNew.Password;
    private void PwdConfirm_Changed(object sender, RoutedEventArgs e) => _vm.ConfirmPassword = PwdConfirm.Password;

    private void Logo_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title = "انتخابِ لوگوی شرکت",
            Filter = "تصویر (*.png;*.jpg;*.jpeg;*.bmp)|*.png;*.jpg;*.jpeg;*.bmp|همه فایل‌ها (*.*)|*.*",
        };
        if (dlg.ShowDialog() == true) _vm.LogoPath = dlg.FileName;
    }
}
