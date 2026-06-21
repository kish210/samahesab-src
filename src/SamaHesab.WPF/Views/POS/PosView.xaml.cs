using System.Windows;
using System.Windows.Controls;

namespace SamaHesab.WPF.Views.POS;

public partial class PosView : UserControl
{
    public PosView()
    {
        InitializeComponent();
        // OPT-11: فوکوسِ همیشگیِ نوارِ بارکد برای اسکنِ پیوسته و سریع
        Loaded += (_, _) => BarcodeBox.Focus();
        // POS-CUSTOMER — F6: فوکوس/بازکردنِ انتخاب‌گرِ مشتری.
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.F6)
            {
                CustomerPicker.Focus();
                CustomerPicker.IsDropDownOpen = true;
                e.Handled = true;
            }
        };
    }

    private void ServerSettings_Click(object sender, RoutedEventArgs e)
    {
        new Shell.ConnectionSettingsWindow { Owner = Window.GetWindow(this) }.ShowDialog();
    }
}
