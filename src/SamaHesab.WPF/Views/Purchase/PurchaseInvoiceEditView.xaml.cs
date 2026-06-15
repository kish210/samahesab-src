using System.Windows.Controls;
using SamaHesab.WPF.ViewModels.Purchase;

namespace SamaHesab.WPF.Views.Purchase;

public partial class PurchaseInvoiceEditView : UserControl
{
    public PurchaseInvoiceEditView()
    {
        InitializeComponent();
        Loaded += (_, _) => ProductCombo.Focus();
        // T10: پس از افزودنِ هر ردیف، فوکوس به نوارِ ورودِ کالا برگردد (ورودِ پیوسته)
        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is PurchaseInvoiceEditViewModel oldVm) oldVm.RowAdded -= FocusEntry;
            if (e.NewValue is PurchaseInvoiceEditViewModel newVm) newVm.RowAdded += FocusEntry;
        };
    }

    private void FocusEntry() => ProductCombo.Focus();
}
