using System.Windows.Controls;
using SamaHesab.WPF.ViewModels.CRM;

namespace SamaHesab.WPF.Views.CRM;

public partial class PersonsListView : UserControl
{
    public PersonsListView()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            if (DataContext is PersonsListViewModel vm) await vm.LoadAsync();
        };
    }
}
