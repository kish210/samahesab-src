using System.Windows;

namespace SamaHesab.WPF.Views.Onboarding;

/// <summary>U-SEC-RECOVERY — نمایشِ یک‌بارهٔ کدِ بازیابی، بلافاصله پس از تعیینِ رمزِ ادمین در
/// ویزاردِ راه‌اندازیِ اولیه. بدونِ دکمهٔ بستن/انصراف؛ فقط با تأییدِ «ذخیره کردم» ادامه می‌دهد.</summary>
public partial class RecoveryCodeWindow : Window
{
    public RecoveryCodeWindow(string recoveryCode)
    {
        InitializeComponent();
        CodeText.Text = recoveryCode;
    }

    private void ChkSaved_Changed(object sender, RoutedEventArgs e) =>
        BtnContinue.IsEnabled = ChkSaved.IsChecked == true;

    private void BtnCopy_Click(object sender, RoutedEventArgs e)
    {
        try { Clipboard.SetText(CodeText.Text); } catch { /* بی‌اثر — کاربر می‌تواند دستی کپی کند */ }
    }

    private void BtnContinue_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }
}
