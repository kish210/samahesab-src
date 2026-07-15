using System.IO;
using System.Windows;
using Microsoft.Win32;
using SamaHesab.Application.Licensing;

namespace SamaHesab.SupportTool;

/// <summary>
/// U-SUPPORT-RESET — نسخهٔ گرافیکیِ ابزارِ صدورِ کدِ ریستِ پشتیبانی (جایگزینِ نسخهٔ کنسولی، به
/// درخواستِ کاربر). فقط دستِ پشتیبانی/وندور اجرا می‌شود؛ کلیدِ خصوصیِ RSA از یک فایلِ PEM محلی
/// (خارج از این پروژه/گیت) انتخاب می‌شود، هرگز در کد نیست.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();

    private void BrowseKey_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "کلیدِ خصوصیِ PEM (*.pem;*.key;*.txt)|*.pem;*.key;*.txt|همهٔ فایل‌ها (*.*)|*.*" };
        if (dlg.ShowDialog() == true) TxtKeyPath.Text = dlg.FileName;
    }

    private void Generate_Click(object sender, RoutedEventArgs e)
    {
        ErrorPanel.Visibility = Visibility.Collapsed;
        ResultPanel.Visibility = Visibility.Collapsed;

        var fingerprint = TxtFingerprint.Text.Trim();
        var keyPath = TxtKeyPath.Text.Trim();

        if (string.IsNullOrWhiteSpace(fingerprint))
        { ShowError("کدِ دستگاهِ مشتری را وارد کنید."); return; }
        if (string.IsNullOrWhiteSpace(keyPath) || !File.Exists(keyPath))
        { ShowError("فایلِ کلیدِ خصوصی را انتخاب کنید (یافت نشد)."); return; }
        if (!int.TryParse(TxtDays.Text.Trim(), out var days) || days <= 0)
        { ShowError("مدتِ اعتبار باید عددِ صحیحِ مثبت باشد."); return; }

        try
        {
            var privateKeyPem = File.ReadAllText(keyPath);
            var now = DateTime.UtcNow;
            var token = new SupportResetToken(fingerprint, now, now.AddDays(days));
            var signature = SupportResetTokenSigner.Sign(token, privateKeyPem);
            var code = new SupportResetTokenDocument(token, signature).ToCode();

            TxtResultCode.Text = code;
            ResultInfo.Text = $"معتبر تا {token.ExpiresUtc:yyyy-MM-dd HH:mm} UTC — فقط برایِ همین دستگاه. این متن را عیناً (کپی/پیست) برایِ مشتری بفرستید.";
            ResultPanel.Visibility = Visibility.Visible;
        }
        catch (Exception ex)
        {
            ShowError("خطا در ساختِ کد — فایلِ کلید معتبر نیست؟ " + ex.Message);
        }
    }

    private void CopyCode_Click(object sender, RoutedEventArgs e)
    {
        try { Clipboard.SetText(TxtResultCode.Text); } catch { /* clipboard occasionally locked by another app */ }
    }

    private void ShowError(string message)
    {
        ErrorText.Text = message;
        ErrorPanel.Visibility = Visibility.Visible;
    }
}
