using MahApps.Metro.Controls;
using SamaHesab.WPF.ViewModels.Shell;

namespace SamaHesab.WPF.Views.Shell;

public partial class MainWindow : MetroWindow
{
    private readonly MainViewModel _vm;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = _vm = viewModel;
        VersionText.Text = $"نسخه {Services.AppVersion.Display} سازمانی";
        Loaded += async (_, _) => await viewModel.LoadAsync();
    }

    /// <summary>منوی تاپ‌بار: کلیک روی دکمه، فهرست بازشونده‌ی همان دکمه را زیر آن باز می‌کند.</summary>
    private void TopMenu_Click(object sender, System.Windows.RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button b && b.ContextMenu is not null)
        {
            b.ContextMenu.DataContext = DataContext;   // تا آیتم‌ها به پرچم‌های ماژول bind شوند
            b.ContextMenu.PlacementTarget = b;
            b.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            b.ContextMenu.IsOpen = true;
        }
    }
}
