using System.Windows;

namespace SamaHesab.WPF.Services;

public static class WindowExtensions
{
    /// <summary>
    /// ShowDialog با ست‌کردنِ امنِ Owner. اگر `Application.Current.MainWindow` در آن لحظه null باشد،
    /// WPF پنجرهٔ نوساخته را خودش MainWindow می‌کند؛ آنگاه `Owner = MainWindow` یعنی «Owner برابرِ خودِ پنجره»
    /// که استثناءِ «Cannot set Owner property to itself» می‌دهد. این متد فقط وقتی Owner را ست می‌کند که
    /// مالکِ معتبر و متفاوت از خودِ پنجره باشد.
    /// </summary>
    public static bool? ShowDialogOwned(this Window win)
    {
        try
        {
            var owner = System.Windows.Application.Current?.MainWindow;
            // فقط مالکِ معتبر، متفاوت از خودِ پنجره و قابلِ‌نمایش (نه بسته/نامرئی) را ست کن.
            if (owner is not null && !ReferenceEquals(owner, win) && owner.IsVisible)
                win.Owner = owner;
        }
        catch { /* مالکِ نامعتبر → بدونِ Owner نمایش بده (نباید کرش کند) */ }
        return win.ShowDialog();
    }
}
