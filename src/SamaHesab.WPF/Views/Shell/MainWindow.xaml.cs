using MahApps.Metro.Controls;
using SamaHesab.WPF.ViewModels.Shell;

namespace SamaHesab.WPF.Views.Shell;

public partial class MainWindow : MetroWindow
{
    private readonly MainViewModel _vm;

    // X6 — قفل/خروجِ خودکار پس از بی‌فعالیتی (امنیتِ تجاری).
    private readonly System.Windows.Threading.DispatcherTimer _idleTimer = new() { Interval = System.TimeSpan.FromSeconds(20) };
    private System.DateTime _lastActivity = System.DateTime.Now;
    private int _idleTimeoutMin;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = _vm = viewModel;
        VersionText.Text = $"نسخه {Services.AppVersion.Display} سازمانی";
        Loaded += async (_, _) => await viewModel.LoadAsync();
        PreviewKeyDown += OnGlobalKeyDown;
        SetupIdleTimeout();
    }

    private void SetupIdleTimeout()
    {
        _idleTimeoutMin = Services.AppSettingsStore.GetGeneral().IdleTimeoutMinutes;
        if (_idleTimeoutMin <= 0) return;   // خاموش
        // هر ورودی، زمان‌سنج را صفر می‌کند.
        PreviewMouseMove += (_, _) => _lastActivity = System.DateTime.Now;
        PreviewMouseDown += (_, _) => _lastActivity = System.DateTime.Now;
        PreviewKeyDown += (_, _) => _lastActivity = System.DateTime.Now;
        _idleTimer.Tick += IdleTimer_Tick;
        _idleTimer.Start();
    }

    private void IdleTimer_Tick(object? sender, System.EventArgs e)
    {
        if (_idleTimeoutMin <= 0) return;
        if ((System.DateTime.Now - _lastActivity).TotalMinutes < _idleTimeoutMin) return;
        _idleTimer.Stop();
        // خروجِ امن: پیام + راه‌اندازیِ مجددِ برنامه به صفحهٔ ورود (نشست بسته می‌شود).
        try
        {
            System.Windows.MessageBox.Show(
                $"به‌خاطرِ {_idleTimeoutMin} دقیقه بی‌فعالیتی، نشست بسته شد. لطفاً دوباره وارد شوید.",
                "خروجِ خودکار", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            var exe = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            if (!string.IsNullOrEmpty(exe))
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(exe) { UseShellExecute = true });
        }
        catch { }
        System.Windows.Application.Current.Shutdown();
    }

    // DL-C1-E: Ctrl+K = فوکوس به جست‌وجوی سراسری (بقیهٔ میان‌برها در XAML InputBindings).
    private void OnGlobalKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.K &&
            (System.Windows.Input.Keyboard.Modifiers & System.Windows.Input.ModifierKeys.Control) == System.Windows.Input.ModifierKeys.Control)
        {
            GlobalSearchBox.Focus();
            e.Handled = true;
        }
    }

    // ── CC-1 — ناوبریِ کیبوردیِ جست‌وجوی سراسری ──
    private SamaHesab.WPF.ViewModels.Shell.MainViewModel? Vm => DataContext as SamaHesab.WPF.ViewModels.Shell.MainViewModel;

    /// <summary>Enter = بازکردنِ نتیجهٔ انتخاب‌شده/اول · ↓ = ورود به فهرست · Esc = بستنِ Popup.</summary>
    private void GlobalSearchBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        switch (e.Key)
        {
            case System.Windows.Input.Key.Enter:
                if (Vm is { } vm1 && vm1.SearchResults.Count > 0) { vm1.OpenSearchResultCommand.Execute(null); e.Handled = true; }
                break;
            case System.Windows.Input.Key.Down:
                if (Vm is { } vm2 && vm2.SearchResults.Count > 0)
                {
                    vm2.IsSearchOpen = true;
                    SearchResultsList.SelectedIndex = 0;
                    var it = SearchResultsList.ItemContainerGenerator.ContainerFromIndex(0) as System.Windows.UIElement;
                    it?.Focus();
                    e.Handled = true;
                }
                break;
            case System.Windows.Input.Key.Escape:
                if (Vm is { } vm3) vm3.IsSearchOpen = false;
                break;
        }
    }

    /// <summary>Enter روی فهرست = بازکردنِ نتیجهٔ انتخاب‌شده.</summary>
    private void SearchResultsList_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == System.Windows.Input.Key.Enter && SearchResultsList.SelectedItem is SamaHesab.WPF.Services.Search.GlobalSearchResult r)
        { Vm?.OpenSearchResultCommand.Execute(r); e.Handled = true; }
    }

    /// <summary>دابل‌کلیک روی نتیجه = بازکردن.</summary>
    private void SearchResult_Activate(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (SearchResultsList.SelectedItem is SamaHesab.WPF.Services.Search.GlobalSearchResult r)
            Vm?.OpenSearchResultCommand.Execute(r);
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
