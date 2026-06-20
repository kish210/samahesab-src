using System.Windows.Controls;
using System.Windows.Input;
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
}
