using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SamaHesab.WPF.ViewModels.Support;

namespace SamaHesab.WPF.Views.Support;

/// <summary>🆘 HC-3 — فرمِ گزارشِ باگ + گرفتنِ اسکرین‌شات.</summary>
public partial class BugReportView : UserControl
{
    public BugReportView() => InitializeComponent();

    private void Capture_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is not BugReportViewModel vm) return;
        try
        {
            // پنجرهٔ اصلی را به تصویر تبدیل می‌کنیم (محتوای برنامه — نه کلِ دسکتاپ).
            var win = Window.GetWindow(this);
            if (win is null) return;
            int w = (int)Math.Max(1, win.ActualWidth);
            int h = (int)Math.Max(1, win.ActualHeight);
            var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
            rtb.Render(win);

            var dlg = new ScreenshotAnnotateWindow(rtb) { Owner = win };
            if (dlg.ShowDialog() == true && dlg.SavedPath is { Length: > 0 })
                vm.SetAttachment(dlg.SavedPath);
        }
        catch (Exception ex)
        {
            MessageBox.Show("گرفتنِ اسکرین‌شات ناموفق بود:\n" + ex.Message, "خطا",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
