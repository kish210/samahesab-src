using System.Windows;
using MediatR;
using SamaHesab.Application.Security.Commands;

namespace SamaHesab.WPF.Views.Shell;

/// <summary>U-SEC-RECOVERY — بازنشانیِ رمزِ فراموش‌شده با کدِ بازیابیِ ساخته‌شده در ویزاردِ اولیه.</summary>
public partial class ForgotPasswordWindow : Window
{
    private readonly IMediator _mediator;
    private readonly int _companyId;

    public bool PasswordReset { get; private set; }

    public ForgotPasswordWindow(IMediator mediator, int companyId, string? prefillUsername = null)
    {
        InitializeComponent();
        _mediator = mediator;
        _companyId = companyId;
        if (!string.IsNullOrWhiteSpace(prefillUsername)) TxtUsername.Text = prefillUsername;
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private async void BtnSubmit_Click(object sender, RoutedEventArgs e)
    {
        ErrorPanel.Visibility = Visibility.Collapsed;

        var username = TxtUsername.Text.Trim();
        var code = TxtRecoveryCode.Text.Trim();
        var pw = PwdNew.Password;

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(code))
        {
            ErrorText.Text = "نامِ کاربری و کدِ بازیابی را وارد کنید.";
            ErrorPanel.Visibility = Visibility.Visible;
            return;
        }
        if (pw != PwdConfirm.Password)
        {
            ErrorText.Text = "رمزِ جدید و تکرارِ آن یکسان نیستند.";
            ErrorPanel.Visibility = Visibility.Visible;
            return;
        }

        BtnSubmit.IsEnabled = false;
        try
        {
            var result = await _mediator.Send(new ResetPasswordWithRecoveryCodeCommand(_companyId, username, code, pw));
            if (!result.Succeeded)
            {
                ErrorText.Text = result.ErrorMessage ?? "خطا در بازیابیِ رمز.";
                ErrorPanel.Visibility = Visibility.Visible;
                return;
            }

            PasswordReset = true;
            DialogResult = true;
            Close();
        }
        finally { BtnSubmit.IsEnabled = true; }
    }
}
