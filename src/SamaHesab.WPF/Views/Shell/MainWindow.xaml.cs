using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows.Controls;
using MahApps.Metro.Controls;
using Telerik.Windows.Controls;
using Telerik.Windows.Controls.Docking;
using SamaHesab.WPF.ViewModels.Shell;

namespace SamaHesab.WPF.Views.Shell;

public partial class MainWindow : MetroWindow
{
    private readonly MainViewModel _vm;
    private readonly Dictionary<WorkspaceTab, RadDocumentPane> _panes = new();
    private bool _syncing;

    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = _vm = viewModel;
        _vm.OpenTabs.CollectionChanged += OpenTabs_CollectionChanged;
        _vm.PropertyChanged += Vm_PropertyChanged;
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

    private void OpenTabs_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
            foreach (WorkspaceTab t in e.NewItems) AddPane(t);
        if (e.OldItems != null)
            foreach (WorkspaceTab t in e.OldItems) RemovePane(t);
    }

    private void AddPane(WorkspaceTab tab)
    {
        if (_panes.ContainsKey(tab)) { ActivatePane(tab); return; }
        var pane = new RadDocumentPane
        {
            Header = tab.Title,
            Content = new ContentControl { Content = tab.Content },
            CanUserClose = tab.CanClose,
            CanFloat = true,
        };
        pane.Tag = tab;
        _panes[tab] = pane;
        docGroup.Items.Add(pane);
        ActivatePane(tab);
    }

    private void RemovePane(WorkspaceTab tab)
    {
        if (!_panes.TryGetValue(tab, out var pane)) return;
        _panes.Remove(tab);
        if (pane.PaneGroup != null) pane.RemoveFromParent();
    }

    private void ActivatePane(WorkspaceTab tab)
    {
        if (_syncing) return;
        if (_panes.TryGetValue(tab, out var pane))
        {
            _syncing = true;
            pane.IsSelected = true;
            pane.IsActive = true;
            _syncing = false;
        }
    }

    private void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.SelectedTab) && _vm.SelectedTab != null)
            ActivatePane(_vm.SelectedTab);
    }

    // User closed a document pane → remove the matching workspace tab.
    private void Docking_Close(object? sender, StateChangeEventArgs e)
    {
        foreach (var pane in e.Panes.OfType<RadDocumentPane>())
            if (pane.Tag is WorkspaceTab tab)
            {
                _panes.Remove(tab);
                if (_vm.OpenTabs.Contains(tab))
                    _vm.CloseTabCommand.Execute(tab);
            }
    }
}
