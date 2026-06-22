using System.Windows;
using SamaHesab.WPF.ViewModels.HRM;

namespace SamaHesab.WPF.Views.HRM;

public partial class AttendanceWorkspaceWindow : Window
{
    public AttendanceWorkspaceWindow(AttendanceWorkspaceViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        Loaded += async (_, _) => await vm.LoadAsync();
    }
}
