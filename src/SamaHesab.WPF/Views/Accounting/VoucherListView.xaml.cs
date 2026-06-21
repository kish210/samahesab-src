using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace SamaHesab.WPF.Views.Accounting;

public partial class VoucherListView : UserControl
{
    public VoucherListView() => InitializeComponent();

    // CC-5 — راست‌کلیک ابتدا همان ردیف را انتخاب کند تا منو روی ردیفِ درست عمل کند.
    private void Grid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var dep = e.OriginalSource as DependencyObject;
        while (dep != null && dep is not DataGridRow) dep = VisualTreeHelper.GetParent(dep);
        if (dep is DataGridRow row) row.IsSelected = true;
    }
}
