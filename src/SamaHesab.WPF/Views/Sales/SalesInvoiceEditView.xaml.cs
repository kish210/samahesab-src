using System.Windows;
using System.Windows.Controls;
using SamaHesab.WPF.Behaviors;
using SamaHesab.WPF.ViewModels.Sales;
using SamaHesab.WPF.Views.Shell;

namespace SamaHesab.WPF.Views.Sales;

public partial class SalesInvoiceEditView : UserControl
{
    public SalesInvoiceEditView()
    {
        InitializeComponent();
        // ورود کیبوردمحور: فوکوس روی نوار بارکد هنگام باز شدن فرم
        Loaded += (_, _) => BarcodeBox.Focus();
        // U-UX-3: Enter در آخرین ستونِ آخرین ردیف → ردیفِ نو + فوکوسِ کالا (ورودِ کلاسیکِ سطربه‌سطر)
        DataGridQuickEntryHelper.EnableEnterToAddRow(ItemsGrid,
            () => (DataContext as SalesInvoiceEditViewModel)?.AddEmptyRowCommand,
            productColumnIndex: 1, lastEditableColumnIndex: 7);
        // OPT-5: میان‌برهای فوکوس — F4 نوار بارکد/کالا · F6 انتخاب مشتری
        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.F4) { BarcodeBox.Focus(); e.Handled = true; }
            else if (e.Key == System.Windows.Input.Key.F6) { CustomerCombo.Focus(); e.Handled = true; }
        };
        // T10: پس از افزودنِ هر ردیف، فوکوس به نوارِ بارکد برگردد (ورودِ پیوسته)
        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is SalesInvoiceEditViewModel oldVm) oldVm.RowAdded -= FocusEntry;
            if (e.NewValue is SalesInvoiceEditViewModel newVm) newVm.RowAdded += FocusEntry;
        };
    }

    private void FocusEntry() => BarcodeBox.Focus();

    private void FocusBarcode_Click(object sender, RoutedEventArgs e) => BarcodeBox.Focus();

    private async void AddCustomer_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new QuickAddCustomerWindow { Owner = Window.GetWindow(this) };
        if (dlg.ShowDialog() == true && DataContext is SalesInvoiceEditViewModel vm)
            await vm.ReloadCustomersAsync(dlg.NewCustomerId);
    }
}
