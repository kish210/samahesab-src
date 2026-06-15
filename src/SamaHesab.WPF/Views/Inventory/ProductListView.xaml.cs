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
}
