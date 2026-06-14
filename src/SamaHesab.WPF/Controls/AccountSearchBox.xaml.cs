using System.Collections;
using System.Windows;

namespace SamaHesab.WPF.Controls;

/// <summary>
/// کامپوننتِ مشترکِ جست‌وجوی هوشمندِ حساب (type-ahead) — FND-5/U13/T3.
/// آیتم‌های `ItemsSource` باید `Id` و `Display` داشته باشند (مثلِ `AccountPick`/`VoucherAccountItem`).
/// مقدارِ انتخابی در `SelectedAccountId` (دوطرفه) قرار می‌گیرد.
/// </summary>
public partial class AccountSearchBox : System.Windows.Controls.UserControl
{
    public AccountSearchBox() => InitializeComponent();

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
