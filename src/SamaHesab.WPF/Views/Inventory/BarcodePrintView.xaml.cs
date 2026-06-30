using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SamaHesab.WPF.Services;
using SamaHesab.WPF.ViewModels.Inventory;

namespace SamaHesab.WPF.Views.Inventory;

public partial class BarcodePrintView : UserControl
{
    public BarcodePrintView()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (DataContext is BarcodePrintViewModel vm)
            {
                await vm.LoadAsync();
                vm.PropertyChanged += (_, _) => RefreshPreview();
                RefreshPreview();
            }
        };
    }

    private BarcodePrintViewModel? Vm => DataContext as BarcodePrintViewModel;

    /// <summary>پیش‌نمایشِ یک برچسب با مقادیرِ جاری.</summary>
    private void RefreshPreview()
    {
        if (Vm is null || PreviewHost is null) return;
        PreviewHost.Content = BarcodeService.BuildLabel(
            Vm.ProductName, Vm.BarcodeValue, Vm.PriceText, Vm.ShowName, Vm.ShowPrice);
    }

    /// <summary>چاپِ Count برچسب در شبکه‌ای با Columns ستون روی چاپگرِ انتخابی.</summary>
    private void Print_Click(object sender, RoutedEventArgs e)
    {
        if (Vm is null) return;
        if (string.IsNullOrWhiteSpace(Vm.BarcodeValue))
        {
            MessageBox.Show("ابتدا کالا را انتخاب یا بارکد را وارد/تولید کنید.", "بارکد",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var dlg = new System.Windows.Controls.PrintDialog();
        if (dlg.ShowDialog() != true) return;

        int count = Vm.Count < 1 ? 1 : Vm.Count;
        int cols = Vm.Columns < 1 ? 1 : Vm.Columns;

        var panel = new WrapPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Width = cols * 195,           // ۱۸۹ عرضِ برچسب + حاشیه
            Background = Brushes.White
        };
        for (int i = 0; i < count; i++)
            panel.Children.Add(BarcodeService.BuildLabel(
                Vm.ProductName, Vm.BarcodeValue, Vm.PriceText, Vm.ShowName, Vm.ShowPrice));

        var size = new Size(dlg.PrintableAreaWidth, double.PositiveInfinity);
        panel.Measure(size);
        panel.Arrange(new Rect(new Point(0, 0), panel.DesiredSize));
        panel.UpdateLayout();
        dlg.PrintVisual(panel, $"بارکد — {Vm.ProductName}");
    }
}
