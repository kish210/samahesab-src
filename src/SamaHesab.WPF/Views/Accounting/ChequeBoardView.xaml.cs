using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SamaHesab.WPF.ViewModels.Accounting;

namespace SamaHesab.WPF.Views.Accounting;

public partial class ChequeBoardView : UserControl
{
    public ChequeBoardView()
    {
        InitializeComponent();
        Loaded += (_, _) => Grid.Focus();   // فوکوس تا میان‌برها بلافاصله کار کنند
    }

    // کیبوردمحور: Enter=وصول · Del=برگشت · F5=بروزرسانی (روی ردیفِ انتخابی).
    private void Grid_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not ChequeBoardViewModel vm) return;
        switch (e.Key)
        {
            case Key.Enter:
                if (vm.ClearSelectedCommand.CanExecute(null)) vm.ClearSelectedCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Delete:
                if (vm.ReturnSelectedCommand.CanExecute(null)) vm.ReturnSelectedCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.F5:
                if (vm.RefreshCommand.CanExecute(null)) vm.RefreshCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }

    // راست‌کلیک ابتدا همان ردیف را انتخاب کند تا منوی راست‌کلیک روی ردیفِ درست عمل کند.
    private void Grid_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var dep = e.OriginalSource as DependencyObject;
        while (dep != null && dep is not DataGridRow) dep = VisualTreeHelper.GetParent(dep);
        if (dep is DataGridRow row) row.IsSelected = true;
    }
}
