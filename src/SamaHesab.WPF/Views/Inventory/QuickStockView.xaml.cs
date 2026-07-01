using System.Windows.Controls;
using SamaHesab.WPF.ViewModels.Inventory;

namespace SamaHesab.WPF.Views.Inventory;

public partial class QuickStockView : UserControl
{
    public QuickStockView()
    {
        InitializeComponent();
        Loaded += async (_, _) => { if (DataContext is QuickStockViewModel vm) await vm.LoadAsync(); };
    }
}
