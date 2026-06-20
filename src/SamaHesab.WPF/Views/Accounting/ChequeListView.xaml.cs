using System.Windows.Controls;
using System.Windows.Input;
using SamaHesab.WPF.ViewModels.Accounting;

namespace SamaHesab.WPF.Views.Accounting;

public partial class ChequeListView : UserControl
{
    public ChequeListView() => InitializeComponent();

    // یکدست با تابلوی چک: Enter روی چکِ انتخابی = وصول (کاهشِ کلیک).
    private void Grid_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && DataContext is ChequeListViewModel vm
            && vm.ClearChequeCommand.CanExecute(null))
        {
            vm.ClearChequeCommand.Execute(null);
            e.Handled = true;
        }
    }
}
