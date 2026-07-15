using System.Windows;
using MediatR;
using SamaHesab.Application.Security.Commands;

namespace SamaHesab.WPF.Views.Shell;

/// <summary>
/// U-SEC-RECOVERY — بازنشانیِ رمزِ فراموش‌شده با کدِ بازیابیِ ساخته‌شده در ویزاردِ اولیه.
/// U-SUPPORT-RESET — اگر آن کد هم گم شده باشد، حالتِ دومی هم دارد: کدِ دستگاه (Fingerprint) به
/// پشتیبانی داده می‌شود، پشتیبانی با ابزارِ آفلاینِ خودش یک کدِ ریستِ کوتاه‌مدتِ مخصوصِ همین دستگاه
/// امضا می‌کند و برمی‌گرداند.
/// </summary>
public partial class ForgotPasswordWindow : Window
{
    private readonly IMediator _mediator;
    private readonly int _companyId;
    private bool _supportMode;

    public bool PasswordReset { get; private set; }

    public ForgotPasswordWindow(IMediator mediator, int companyId, string? prefillUsername = null)
    {
        InitializeComponent();
        _mediator = mediator;
        _companyId = companyId;
        if (!string.IsNullOrWhiteSpace(prefillUsername)) TxtUsername.Text = prefillUsername;
        TxtFingerprint.Text = App.GetService<Services.LicenseService>().MachineFingerprint;
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void ToggleSupportMode_Click(object sender, RoutedEventArgs e)
    {
        _supportMode = !_supportMode;
        LblRecoveryCode.Visibility = RowRecoveryCode.Visibility = _supportMode ? Visibility.Collapsed : Visibility.Visible;
        SupportPanel.Visibility = _supportMode ? Visibility.Visible : Visibility.Collapsed;
        BtnToggleSupportMode.Content = _supportMode
            ? "بازگشت به وارد‌کردنِ کدِ بازیابیِ محلی"
            : "کدِ بازیابی را هم گم کرده‌ام — کمکِ پشتیبانی";
    }

    private void CopyFingerprint_Click(object sender, RoutedEventArgs e)
    {
        try { Clipboard.SetText(TxtFingerprint.Text); } catch { /* clipboard occasionally locked by another app */ }
    }

    private async void BtnSubmit_Click(object sender, RoutedEventArgs e)
    {
        ErrorPanel.Visibility = Visibility.Collapsed;

        var username = TxtUsername.Text.Trim();
        var pw = PwdNew.Password;

        if (string.IsNullOrWhiteSpace(username))
        {
            ErrorText.Text = "نامِ کاربری را وارد کنید.";
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
            var result = _supportMode
                ? await _mediator.Send(new ResetPasswordWithSupportTokenCommand(_companyId, username, TxtSupportToken.Text.Trim(), pw))
                : await _mediator.Send(new ResetPasswordWithRecoveryCodeCommand(_companyId, username, TxtRecoveryCode.Text.Trim(), pw));
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
