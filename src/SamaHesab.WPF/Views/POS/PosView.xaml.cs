using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace SamaHesab.WPF.Views.POS;

public partial class PosView : UserControl
{
    private TextBox? _customerEditBox;

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

        // U-POS-8 — جست‌وجویِ contains رویِ نامِ مشتری (به‌جایِ prefix-matchِ پیش‌فرضِ WPF)، هم‌راستا با AccountSearchBox.
        CustomerPicker.Loaded += (_, _) =>
        {
            _customerEditBox ??= CustomerPicker.Template.FindName("PART_EditableTextBox", CustomerPicker) as TextBox;
            if (_customerEditBox is not null) _customerEditBox.TextChanged += CustomerEditBox_TextChanged;
        };
        CustomerPicker.DropDownClosed += (_, _) =>
        {
            if (CustomerPicker.ItemsSource is null) return;
            var view = CollectionViewSource.GetDefaultView(CustomerPicker.ItemsSource);
            if (view is not null) view.Filter = null;
        };
    }

    private void CustomerEditBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (CustomerPicker.ItemsSource is null) return;
        var view = CollectionViewSource.GetDefaultView(CustomerPicker.ItemsSource);
        if (view is null) return;
        var text = _customerEditBox?.Text ?? string.Empty;

        if (string.IsNullOrWhiteSpace(text)) { view.Filter = null; return; }
        if (CustomerPicker.SelectedItem is ViewModels.POS.PosCustomer sel && sel.Name == text) return;

        var q = text.Trim();
        view.Filter = o => o is ViewModels.POS.PosCustomer c && c.Name.Contains(q, System.StringComparison.OrdinalIgnoreCase);
        if (!CustomerPicker.IsDropDownOpen) CustomerPicker.IsDropDownOpen = true;
    }

    private void ServerSettings_Click(object sender, RoutedEventArgs e)
    {
        new Shell.ConnectionSettingsWindow { Owner = Window.GetWindow(this) }.ShowDialog();
    }
}
