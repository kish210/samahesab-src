using System.Windows.Controls;
using SamaHesab.WPF.ViewModels.Reports;

namespace SamaHesab.WPF.Views.Reports;

public partial class IncomeReportView : UserControl
{
    public IncomeReportView()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (DataContext is IncomeReportViewModel vm) await vm.LoadAsync();
        };
    }
}
