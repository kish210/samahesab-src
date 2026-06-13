using System.Windows;
using System.Windows.Controls;

namespace SamaHesab.WPF.Views.Inventory;

public partial class ProductListView : UserControl
{
    public ProductListView()
    {
        InitializeComponent();
        // OPT-9: دابل‌کلیک روی ردیف = ویرایش کالا
        grid.MouseDoubleClick += (_, _) =>
        {
            if (DataContext is ViewModels.Inventory.ProductListViewModel vm && vm.EditProductCommand.CanExecute(null))
                vm.EditProductCommand.Execute(null);
        };
    }

    private void ExportExcel_Click(object sender, RoutedEventArgs e)
        => MessageBox.Show("برای خروجی اکسل از «انبار ← گزارش موجودی/ارزش انبار» استفاده کنید.",
            "خروجی اکسل", MessageBoxButton.OK, MessageBoxImage.Information);
}
