using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Threading;
using SamaHesab.WPF.ViewModels.Accounting;

namespace SamaHesab.WPF.Views.Accounting;

public partial class VoucherEditView : UserControl
{
    private VoucherEditViewModel? _vm;
    private TextBox? _accEditBox;   // BUG-1 — جعبهٔ متنیِ کمبوی حساب (برای فیلترِ contains)

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
            ClearAccFilter();   // ردیفِ تازه: فهرستِ کامل
            AccCombo.Focus();
            System.Windows.Input.Keyboard.Focus(AccCombo);
        }));

    // BUG-1 — فیلترِ «contains» روی کمبوی حساب: تایپِ «سرمایه» یا کدِ میانی، فهرست را فیلتر می‌کند
    // (نه فقط پرشِ prefix). زنجیرهٔ کیبوردیِ OPT-ACC-1 حفظ می‌شود: Enter موردِ اول/انتخاب‌شده را قطعی می‌کند.
    private void AccCombo_Loaded(object sender, RoutedEventArgs e)
    {
        if (_accEditBox is not null) return;
        _accEditBox = AccCombo.Template.FindName("PART_EditableTextBox", AccCombo) as TextBox;
        if (_accEditBox is not null) _accEditBox.TextChanged += AccEditBox_TextChanged;
    }

    private void AccEditBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        var view = CollectionViewSource.GetDefaultView(AccCombo.ItemsSource);
        if (view is null) return;
        var q = (_accEditBox?.Text ?? "").Trim();
        if (q.Length == 0) { view.Filter = null; return; }

        view.Filter = o => o is VoucherAccountItem a
            && ((a.Code ?? "").Contains(q, StringComparison.OrdinalIgnoreCase)
             || (a.Name ?? "").Contains(q, StringComparison.OrdinalIgnoreCase));

        // فقط وقتی کاربر در حالِ تایپ است دراپ‌داون باز شود (نه هنگامِ ستِ برنامه‌ای متن).
        if (!AccCombo.IsDropDownOpen && AccCombo.IsKeyboardFocusWithin) AccCombo.IsDropDownOpen = true;
    }

    private void ClearAccFilter()
    {
        var view = CollectionViewSource.GetDefaultView(AccCombo.ItemsSource);
        if (view is not null) view.Filter = null;
    }

    // OPT-ACC-1 — زنجیرهٔ کیبورد در نوارِ ورود: Enter هر فیلد را به فیلدِ بعدی می‌برد
    // (حساب → شرح → بدهکار → بستانکار → افزودن) تا ورودِ سند کاملاً کیبوردمحور و بدونِ ماوس باشد.
    private void AccCombo_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        // چون پرشِ prefix خاموش است، Enter باید انتخاب را قطعی کند: موردِ انتخاب‌شده (با فلش)
        // یا — اگر چیزی انتخاب نشده — نخستین موردِ فیلترشده. سپس فهرست بسته و به «شرح» می‌رویم.
        if (AccCombo.SelectedItem is null)
        {
            var view = CollectionViewSource.GetDefaultView(AccCombo.ItemsSource);
            var first = view?.Cast<object>().FirstOrDefault();
            if (first is not null) AccCombo.SelectedItem = first;
        }
        if (AccCombo.IsDropDownOpen) AccCombo.IsDropDownOpen = false;
        ClearAccFilter();
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
