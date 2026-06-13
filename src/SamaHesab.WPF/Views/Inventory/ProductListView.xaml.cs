using System.Windows;
using System.Windows.Controls;

namespace SamaHesab.WPF.Views.Inventory;

public partial class ProductListView : UserControl
{
    public ProductListView() => InitializeComponent();

    private void ExportExcel_Click(object sender, RoutedEventArgs e)
        => MessageBox.Show("برای خروجی اکسل از «انبار ← گزارش موجودی/ارزش انبار» استفاده کنید.",
            "خروجی اکسل", MessageBoxButton.OK, MessageBoxImage.Information);
}
