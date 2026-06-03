using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Telerik.Windows.Controls;

namespace SamaHesab.WPF.Views.Inventory;

public partial class ProductListView : UserControl
{
    public ProductListView() => InitializeComponent();

    private void ExportExcel_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Filter = "Excel Workbook (*.xlsx)|*.xlsx",
            FileName = "Products.xlsx"
        };
        if (dialog.ShowDialog() != true) return;

        try
        {
            using var stream = dialog.OpenFile();
            grid.ExportToXlsx(stream, new GridViewDocumentExportOptions
            {
                ShowColumnHeaders = true,
                ShowColumnFooters = false,
                ShowGroupFooters = false
            });
            MessageBox.Show("خروجی اکسل با موفقیت ذخیره شد.", "خروجی اکسل",
                MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (System.Exception ex)
        {
            MessageBox.Show("خطا در خروجی اکسل: " + ex.Message, "خطا",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
