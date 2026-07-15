using System.Windows;
using MediatR;
using SamaHesab.WPF.ViewModels.Shell;

namespace SamaHesab.WPF.Views.Shell;

public partial class LoginWindow : Window
{
    private readonly LoginViewModel _vm;
    private readonly IMediator _mediator;

    public LoginWindow(LoginViewModel viewModel, IMediator mediator)
    {
        InitializeComponent();
        _vm = viewModel;
        _mediator = mediator;
        DataContext = viewModel;
        VersionText.Text = $"نسخه {Services.AppVersion.Display}  |  © ۱۴۰۴ سماع رایانه کیش";
        Resources["BoolVis"] = new System.Windows.Controls.BooleanToVisibilityConverter();
        Loaded += (_, _) =>
        {
            TxtUsername?.Focus();
            if (_vm.IsApiMode) BtnSettings.Content = "⚙  تنظیمات اتصال به سرور";
            else
            {
                // U-SEC-RECOVERY — بازیابیِ رمز فقط برایِ ورودِ DB-محورِ حسابداری معنا دارد؛ کلاینت‌هایِ
                // API-محور (POS/رستوران) به DB دسترسیِ مستقیم ندارند تا ResetPasswordWithRecoveryCode
                // بتواند مستقیماً اجرا شود.
                BtnForgotPassword.Visibility = Visibility.Visible;
                // U-MULTI-COMPANY-1 — همان‌طور، ساختِ شرکتِ نو نیازِ دسترسیِ مستقیمِ DB دارد.
                BtnNewCompany.Visibility = Visibility.Visible;
                _ = _vm.LoadCompaniesAsync();
            }
        };
    }

    private void PwdBox_PasswordChanged(object sender, RoutedEventArgs e) =>
        _vm.Password = PwdBox.Password;

    private void CloseButton_Click(object sender, RoutedEventArgs e) =>
        System.Windows.Application.Current.Shutdown();

    private void ForgotPassword_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new ForgotPasswordWindow(_mediator, _vm.SelectedCompanyId, _vm.Username) { Owner = this };
        if (dlg.ShowDialog() == true && dlg.PasswordReset)
            System.Windows.MessageBox.Show(this, "رمزِ عبورِ جدید تنظیم شد. اکنون می‌توانید با آن وارد شوید.",
                "بازیابیِ رمز", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    /// <summary>U-MULTI-COMPANY-1 — بازکردنِ ویزاردِ راه‌اندازی در حالتِ «شرکتِ جدید» (چند شرکت
    /// در یک DBِ مشترک)، بدونِ تأثیر روی شرکتِ فعلی/سشنِ لاگین.</summary>
    private async void NewCompany_Click(object sender, RoutedEventArgs e)
    {
        var vm = App.GetService<SamaHesab.WPF.ViewModels.Onboarding.FirstRunWizardViewModel>();
        vm.IsNewCompanyMode = true;
        new SamaHesab.WPF.Views.Onboarding.FirstRunWizardWindow(vm) { Owner = this }.ShowDialog();
        if (vm.CreatedCompanyId is int newId)
        {
            await _vm.LoadCompaniesAsync();
            _vm.SelectedCompanyId = newId;
        }
    }

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
