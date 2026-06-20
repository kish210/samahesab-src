using System.Windows.Controls;
using System.Windows.Input;
using SamaHesab.WPF.ViewModels.Accounting;

namespace SamaHesab.WPF.Views.Accounting;

public partial class VoucherApprovalsView : UserControl
{
    public VoucherApprovalsView()
    {
        InitializeComponent();
        // فوکوس به گرید تا میان‌برهای کیبورد (Enter/Del/F5) بلافاصله کار کنند.
        Loaded += (_, _) => Grid.Focus();
    }

    // کیبوردمحور: Enter=تأیید · Del=رد · F5=بروزرسانی (روی ردیفِ انتخابی).
    // در PreviewKeyDown می‌گیریم تا گرید Enter را به جابه‌جاییِ ردیف تبدیل نکند.
    private void Grid_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not VoucherApprovalsViewModel vm) return;
        switch (e.Key)
        {
            case Key.Enter:
                if (vm.ApproveSelectedCommand.CanExecute(null)) vm.ApproveSelectedCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.Delete:
                if (vm.RejectSelectedCommand.CanExecute(null)) vm.RejectSelectedCommand.Execute(null);
                e.Handled = true;
                break;
            case Key.F5:
                if (vm.RefreshCommand.CanExecute(null)) vm.RefreshCommand.Execute(null);
                e.Handled = true;
                break;
        }
    }
}
