using System.Windows.Controls;
using System.Windows.Data;
using SamaHesab.WPF.ViewModels.Purchase;

namespace SamaHesab.WPF.Views.Purchase;

public partial class PurchaseReportView : UserControl
{
    public PurchaseReportView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => Hook();
        Hook();
    }

    private PurchaseReportViewModel? _vm;

    private void Hook()
    {
        if (_vm != null) _vm.HeadersChanged -= RebuildColumns;
        _vm = DataContext as PurchaseReportViewModel;
        if (_vm != null) { _vm.HeadersChanged += RebuildColumns; RebuildColumns(); }
    }

    private void RebuildColumns()
    {
        grid.Columns.Clear();
        if (_vm == null) return;
        var headers = _vm.Headers;
        for (int i = 0; i < headers.Length; i++)
        {
            var col = new DataGridTextColumn
            {
                Header = headers[i],
                Binding = new Binding($"[{i}]"),
                Width = i == 0 ? new DataGridLength(1, DataGridLengthUnitType.Star)
                               : new DataGridLength(150)
            };
            if (i > 0)
            {
                var style = new System.Windows.Style(typeof(DataGridCell));
                style.Setters.Add(new System.Windows.Setter(System.Windows.FrameworkElement.FlowDirectionProperty,
                    System.Windows.FlowDirection.LeftToRight));
                style.Setters.Add(new System.Windows.Setter(System.Windows.Controls.Control.HorizontalContentAlignmentProperty,
                    System.Windows.HorizontalAlignment.Left));
                col.CellStyle = style;
            }
            grid.Columns.Add(col);
        }
    }
}
