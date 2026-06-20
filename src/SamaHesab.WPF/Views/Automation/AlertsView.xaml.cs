using System.Windows.Controls;
using System.Windows.Input;
using SamaHesab.WPF.ViewModels.Automation;

namespace SamaHesab.WPF.Views.Automation;

public partial class AlertsView : UserControl
{
    public AlertsView() => InitializeComponent();

    // Enter → باز کردنِ اعلانِ انتخابی (ناوبری به منبع)؛ F5 → تازه‌سازی.
    private void AlertList_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not AlertsViewModel vm) return;
        if (e.Key == Key.Enter) { vm.OpenSelectedCommand.Execute(null); e.Handled = true; }
        else if (e.Key == Key.F5) { if (vm.RefreshCommand.CanExecute(null)) vm.RefreshCommand.Execute(null); e.Handled = true; }
    }

    private void AlertList_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is AlertsViewModel vm) vm.OpenSelectedCommand.Execute(null);
    }
}
