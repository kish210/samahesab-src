using System.Windows;
using MediatR;
using SamaHesab.Application.Security.Commands;

namespace SamaHesab.WPF.Views.Shell;

/// <summary>
/// SR-6 — پنجرهٔ اجباریِ تغییرِ رمز؛ بدونِ دکمهٔ بستن/انصراف نمایش داده می‌شود
/// (اگر کاربر آن را ببندد، ورود لغو می‌شود — LoginViewModel این را مدیریت می‌کند).
/// </summary>
public partial class ForcePasswordChangeWindow : Window
{
    private readonly IMediator _mediator;
    private readonly int _userId;

    public bool PasswordChanged { get; private set; }

    public ForcePasswordChangeWindow(IMediator mediator, int userId)
    {
        InitializeComponent();
        _mediator = mediator;
        _userId = userId;
    }

    private async void BtnSubmit_Click(object sender, RoutedEventArgs e)
    {
        ErrorPanel.Visibility = Visibility.Collapsed;
        var pw = PwdNew.Password;
        if (pw != PwdConfirm.Password)
        {
            ErrorText.Text = "رمزِ جدید و تکرارِ آن یکسان نیستند.";
            ErrorPanel.Visibility = Visibility.Visible;
            return;
        }

        BtnSubmit.IsEnabled = false;
        try
        {
            var result = await _mediator.Send(new ChangeUserPasswordCommand(_userId, pw));
            if (!result.Succeeded)
            {
                ErrorText.Text = result.ErrorMessage ?? "خطا در تغییرِ رمز.";
                ErrorPanel.Visibility = Visibility.Visible;
                return;
            }

            PasswordChanged = true;
            DialogResult = true;
            Close();
        }
        finally { BtnSubmit.IsEnabled = true; }
    }
}
