using System.Collections;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace SamaHesab.WPF.Controls;

/// <summary>
/// کامپوننتِ مشترکِ جست‌وجوی هوشمندِ حساب/کالا (type-ahead) — FND-5/U13/T3.
/// آیتم‌های <see cref="ItemsSource"/> باید `Id` و `Display` داشته باشند (مثلِ AccountPick/VoucherAccountItem/ProductPick).
/// تایپ → فیلترِ زندهٔ contains روی Display (با نرمال‌سازیِ رقمِ فارسی)؛ مقدارِ انتخابی در <see cref="SelectedAccountId"/>.
/// </summary>
public partial class AccountSearchBox : UserControl
{
    private TextBox? _editBox;
    private bool _suppress;

    public AccountSearchBox()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Combo.DropDownClosed += (_, _) => ClearFilter();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _editBox ??= Combo.Template.FindName("PART_EditableTextBox", Combo) as TextBox;
        if (_editBox is not null)
        {
            _editBox.TextChanged -= OnTextChanged;
            _editBox.TextChanged += OnTextChanged;
        }
    }

    private void OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppress || Combo.ItemsSource is null) return;
        var text = _editBox?.Text ?? string.Empty;
        var view = CollectionViewSource.GetDefaultView(Combo.ItemsSource);
        if (view is null) return;

        if (string.IsNullOrWhiteSpace(text))
        {
            view.Filter = null;
            return;
        }

        // اگر متن دقیقاً برابرِ آیتمِ انتخاب‌شده است (پس از انتخاب)، فیلتر لازم نیست.
        if (Combo.SelectedItem is not null && Normalize(GetDisplay(Combo.SelectedItem)) == Normalize(text))
            return;

        var q = Normalize(text);
        view.Filter = o => Normalize(GetDisplay(o)).Contains(q);
        if (!Combo.IsDropDownOpen) Combo.IsDropDownOpen = true;
    }

    private void ClearFilter()
    {
        if (Combo.ItemsSource is null) return;
        var view = CollectionViewSource.GetDefaultView(Combo.ItemsSource);
        if (view is not null) { _suppress = true; view.Filter = null; _suppress = false; }
    }

    private static string GetDisplay(object? item)
    {
        if (item is null) return string.Empty;
        var p = item.GetType().GetProperty("Display");
        return p?.GetValue(item)?.ToString() ?? item.ToString() ?? string.Empty;
    }

    /// <summary>کوچک‌سازی + نرمال‌سازیِ ارقامِ فارسی/عربی به لاتین (تا «۱۰۱» و «101» یکسان جست‌وجو شوند).</summary>
    private static string Normalize(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        var chars = input.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            var c = chars[i];
            if (c >= '۰' && c <= '۹') chars[i] = (char)('0' + (c - '۰'));
            else if (c >= '٠' && c <= '٩') chars[i] = (char)('0' + (c - '٠'));
        }
        return new string(chars).ToLowerInvariant().Trim();
    }

    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable), typeof(AccountSearchBox),
            new PropertyMetadata(null));

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public static readonly DependencyProperty SelectedAccountIdProperty =
        DependencyProperty.Register(nameof(SelectedAccountId), typeof(int?), typeof(AccountSearchBox),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public int? SelectedAccountId
    {
        get => (int?)GetValue(SelectedAccountIdProperty);
        set => SetValue(SelectedAccountIdProperty, value);
    }
}
