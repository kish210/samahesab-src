using System.Windows;
using System.Windows.Controls;
using SamaHesab.WPF.ViewModels.Sales;
using SamaHesab.WPF.Views.Shell;

namespace SamaHesab.WPF.Views.Sales;

public partial class SalesInvoiceEditView : UserControl
{
    public SalesInvoiceEditView() => InitializeComponent();

    private async void AddCustomer_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new QuickAddCustomerWindow { Owner = Window.GetWindow(this) };
        if (dlg.ShowDialog() == true && DataContext is SalesInvoiceEditViewModel vm)
            await vm.ReloadCustomersAsync(dlg.NewCustomerId);
    }
}
