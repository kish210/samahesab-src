using System;
using System.IO;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SamaHesab.WPF.Views.Support;

/// <summary>
/// 🆘 HC-3 — ابزارِ اسکرین‌شات: روی تصویرِ گرفته‌شده خط بکشید/هایلایت کنید و الصاق کنید.
/// خروجی PNG در «My Documents\SamaHesab\پیوست‌ها» ذخیره و مسیرش در <see cref="SavedPath"/> برمی‌گردد.
/// </summary>
public partial class ScreenshotAnnotateWindow : Window
{
    private readonly BitmapSource _shot;
    public string? SavedPath { get; private set; }

    public ScreenshotAnnotateWindow(BitmapSource shot)
    {
        InitializeComponent();
        _shot = shot;
        ShotImage.Source = shot;
        ShotImage.Width = shot.PixelWidth;
        ShotImage.Height = shot.PixelHeight;
        Ink.Width = shot.PixelWidth;
        Ink.Height = shot.PixelHeight;
        UsePen();
    }

    private void UsePen()
    {
        Ink.EditingMode = InkCanvasEditingMode.Ink;
        Ink.DefaultDrawingAttributes = new DrawingAttributes
        { Color = Colors.Red, Width = 3, Height = 3, FitToCurve = true };
    }

    private void Pen_Click(object sender, RoutedEventArgs e) => UsePen();

    private void Highlight_Click(object sender, RoutedEventArgs e)
    {
        Ink.EditingMode = InkCanvasEditingMode.Ink;
        Ink.DefaultDrawingAttributes = new DrawingAttributes
        {
            Color = Color.FromArgb(110, 255, 235, 0), // زردِ نیمه‌شفاف
            Width = 18, Height = 18, IsHighlighter = true, StylusTip = StylusTip.Rectangle
        };
    }

    private void Erase_Click(object sender, RoutedEventArgs e)
        => Ink.EditingMode = InkCanvasEditingMode.EraseByStroke;

    private void Clear_Click(object sender, RoutedEventArgs e) => Ink.Strokes.Clear();

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            int w = _shot.PixelWidth, h = _shot.PixelHeight;
            var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
            CanvasRoot.Measure(new Size(w, h));
            CanvasRoot.Arrange(new Rect(new Size(w, h)));
            rtb.Render(CanvasRoot);

            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SamaHesab", "پیوست‌ها");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, $"shot_{DateTime.Now:yyyyMMdd_HHmmss}.png");
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(rtb));
            using (var fs = File.Create(path)) enc.Save(fs);

            SavedPath = path;
            DialogResult = true;
        }
        catch (Exception ex)
        {
            MessageBox.Show("ذخیرهٔ اسکرین‌شات ناموفق بود:\n" + ex.Message, "خطا",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
