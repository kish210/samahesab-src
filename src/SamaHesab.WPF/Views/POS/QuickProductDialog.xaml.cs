using System.Globalization;
using System.Windows;
using System.Windows.Input;
using SamaHesab.Domain.Enums;

namespace SamaHesab.WPF.Views.POS;

/// <summary>ثبتِ فوریِ کالا/خدمات از داخلِ صندوقِ فروش (بدونِ ترکِ صفحه).</summary>
public partial class QuickProductDialog : Window
{
    public string ProductName { get; private set; } = string.Empty;
    public string? ProductCode { get; private set; }
    public string? Barcode { get; private set; }
    public decimal SalePrice { get; private set; }
    public decimal PurchasePrice { get; private set; }
    public decimal TaxRate { get; private set; }
    public ProductType ProductType { get; private set; } = ProductType.Product;

    public QuickProductDialog()
    {
        InitializeComponent();
        Loaded += (_, _) => TxtName.Focus();
    }

    private static decimal Parse(string? s)
        => decimal.TryParse((s ?? "").Replace(",", "").Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0;

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        var name = TxtName.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(name))
        {
            TxtError.Text = "نام الزامی است.";
            TxtError.Visibility = Visibility.Visible;
            TxtName.Focus();
            return;
        }

        ProductName = name;
        ProductCode = string.IsNullOrWhiteSpace(TxtCode.Text) ? null : TxtCode.Text.Trim();
        Barcode = string.IsNullOrWhiteSpace(TxtBarcode.Text) ? null : TxtBarcode.Text.Trim();
        SalePrice = Parse(TxtSale.Text);
        PurchasePrice = Parse(TxtPurchase.Text);
        TaxRate = Parse(TxtTax.Text);
        ProductType = RbService.IsChecked == true ? ProductType.Service : ProductType.Product;

        DialogResult = true;
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { BtnSave_Click(this, new RoutedEventArgs()); e.Handled = true; }
        else if (e.Key == Key.Escape) { DialogResult = false; e.Handled = true; }
        base.OnPreviewKeyDown(e);
    }
}
