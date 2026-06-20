using System.Windows.Controls;
using System.Windows.Input;
using SamaHesab.WPF.ViewModels.Treasury;

namespace SamaHesab.WPF.Views.Treasury;

public partial class ReceivablesView : UserControl
{
    public ReceivablesView()
    {
        InitializeComponent();
        Loaded += (_, _) => RecvGrid.Focus();   // فوکوس تا میان‌برها بلافاصله کار کنند
    }

    // کیبوردمحور روی هر گرید: Enter=کامل · Ctrl+Enter=مبلغِ دلخواه.
    private void Grid_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not ReceivablesViewModel vm) return;
        var custom = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
        var isReceivables = ReferenceEquals(sender, RecvGrid);

        if (e.Key == Key.Enter)
        {
            ICommand cmd = (isReceivables, custom) switch
            {
                (true, false)  => vm.ReceiveFullSelectedCommand,
                (true, true)   => vm.ReceiveCustomSelectedCommand,
                (false, false) => vm.PayFullSelectedCommand,
                (false, true)  => vm.PayCustomSelectedCommand,
            };
            if (cmd.CanExecute(null)) cmd.Execute(null);
            e.Handled = true;
        }
    }
}
