using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using SamaHesab.WPF.ViewModels.Accounting;

namespace SamaHesab.WPF.Views.Accounting;

public partial class VoucherEditView : UserControl
{
    private VoucherEditViewModel? _vm;

    public VoucherEditView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    // T10 — ردیفِ ورودِ سریع: پس از افزودنِ هر ردیف، فوکوس به کمبوی حساب برگردد تا ورودِ کیبوردی پیوسته باشد.
    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_vm is not null) _vm.RowAdded -= FocusAccount;
        _vm = DataContext as VoucherEditViewModel;
        if (_vm is not null) _vm.RowAdded += FocusAccount;
    }

    private void FocusAccount()
        => Dispatcher.BeginInvoke(DispatcherPriority.Input, new System.Action(() =>
        {
            AccCombo.Focus();
            System.Windows.Input.Keyboard.Focus(AccCombo);
        }));
}
