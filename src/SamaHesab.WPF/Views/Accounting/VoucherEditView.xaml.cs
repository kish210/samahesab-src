using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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

    // OPT-ACC-1 — زنجیرهٔ کیبورد در نوارِ ورود: Enter هر فیلد را به فیلدِ بعدی می‌برد
    // (حساب → شرح → بدهکار → بستانکار → افزودن) تا ورودِ سند کاملاً کیبوردمحور و بدونِ ماوس باشد.
    private void AccCombo_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        // اگر فهرستِ بازِ جست‌وجو باز است، Enter موردِ هایلایت را قطعی و فهرست را می‌بندد، سپس به «شرح» می‌رویم.
        if (AccCombo.IsDropDownOpen) AccCombo.IsDropDownOpen = false;
        DescBox.Focus();
        e.Handled = true;
    }

    private void DescBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        DebitBox.Focus();
        e.Handled = true;
    }

    private void DebitBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        CreditBox.Focus();
        e.Handled = true;
        // «بستانکار» با Enter ردیف را اضافه می‌کند (InputBinding موجود)؛ پس از افزودن، فوکوس به حساب برمی‌گردد.
    }

    // هنگامِ فوکوس‌گرفتنِ فیلدِ مبلغ، کلِ متن انتخاب شود تا کاربر روی «۰» تایپ کند نه کنارش (کاهشِ کلیک/Backspace).
    private void NumBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is TextBox tb)
            Dispatcher.BeginInvoke(DispatcherPriority.Input, new System.Action(tb.SelectAll));
    }

    // کلیدِ Delete روی ردیفِ انتخاب‌شدهٔ گرید = حذفِ همان ردیف (بدونِ نیاز به ماوس/دکمهٔ ✕).
    private void ItemsGrid_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Delete) return;
        if (_vm is null || ItemsGrid.SelectedItem is not VoucherItemRow row) return;
        if (_vm.RemoveRowCommand.CanExecute(row))
        {
            _vm.RemoveRowCommand.Execute(row);
            e.Handled = true;
        }
    }
}
