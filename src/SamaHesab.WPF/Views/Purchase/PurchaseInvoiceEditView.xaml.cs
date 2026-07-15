using System.Windows.Controls;
using SamaHesab.WPF.Behaviors;
using SamaHesab.WPF.ViewModels.Purchase;

namespace SamaHesab.WPF.Views.Purchase;

public partial class PurchaseInvoiceEditView : UserControl
{
    public PurchaseInvoiceEditView()
    {
        InitializeComponent();
        Loaded += (_, _) => SupplierCombo.Focus();
        // U-UX-3: Enter در آخرین ستونِ آخرین ردیف → ردیفِ نو + فوکوسِ کالا (ورودِ کلاسیکِ سطربه‌سطر)
        DataGridQuickEntryHelper.EnableEnterToAddRow(ItemsGrid,
            () => (DataContext as PurchaseInvoiceEditViewModel)?.AddEmptyRowCommand,
            productColumnIndex: 1, lastEditableColumnIndex: 7);
        // T10: پس از افزودنِ هر ردیف، فوکوس به نوارِ بارکد برگردد (ورودِ پیوسته)
        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is PurchaseInvoiceEditViewModel oldVm) oldVm.RowAdded -= FocusEntry;
            if (e.NewValue is PurchaseInvoiceEditViewModel newVm) newVm.RowAdded += FocusEntry;
        };
    }

    private void FocusEntry() => BarcodeBox.Focus();

    private void FocusBarcode_Click(object sender, System.Windows.RoutedEventArgs e) => BarcodeBox.Focus();
}
