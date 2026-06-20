using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using SamaHesab.WPF.Services.Search;

namespace SamaHesab.WPF.Views.Shell;

/// <summary>
/// CC-2 (UX_ROADMAP) — Command Palette مودال (Ctrl+K). دستورهای ناوبری + نتایجِ زندهٔ CC-1 را
/// ادغام می‌کند؛ فیلترِ تایپی، کیبورد-اول، و اجرا با callbackِ مشترک (همان OpenSearchResult).
/// </summary>
public partial class CommandPaletteWindow : Window
{
    private readonly IReadOnlyList<GlobalSearchResult> _commands;
    private readonly IGlobalSearchService _search;
    private readonly Action<GlobalSearchResult> _onExecute;
    private CancellationTokenSource? _cts;

    public CommandPaletteWindow(IReadOnlyList<GlobalSearchResult> commands,
        IGlobalSearchService search, Action<GlobalSearchResult> onExecute)
    {
        InitializeComponent();
        _commands = commands;
        _search = search;
        _onExecute = onExecute;
        Loaded += (_, _) => { Render(_commands); PaletteBox.Focus(); };
    }

    private void Render(IEnumerable<GlobalSearchResult> items)
    {
        ResultsList.ItemsSource = items.ToList();
        if (ResultsList.Items.Count > 0) ResultsList.SelectedIndex = 0;
    }

    private void PaletteBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        _cts?.Cancel();
        var term = (PaletteBox.Text ?? string.Empty).Trim();

        // دستورهای ناوبری: فیلترِ فوریِ سمتِ کلاینت (یا همه، وقتی خالی است).
        var cmds = string.IsNullOrEmpty(term)
            ? _commands
            : _commands.Where(c => c.Title.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();
        Render(cmds);

        // نتایجِ دادهٔ زنده (CC-1) با debounce، سپس به انتهای دستورها اضافه می‌شوند.
        if (term.Length < 2) return;
        var cts = new CancellationTokenSource();
        _cts = cts;
        _ = RunDataSearchAsync(term, cmds.ToList(), cts.Token);
    }

    private async System.Threading.Tasks.Task RunDataSearchAsync(string term, List<GlobalSearchResult> cmds, CancellationToken ct)
    {
        try
        {
            await System.Threading.Tasks.Task.Delay(220, ct);
            var data = await _search.SearchAsync(term, perGroupCap: 6, ct);
            if (ct.IsCancellationRequested) return;
            Dispatcher.Invoke(() =>
            {
                if (_cts is not null && _cts.IsCancellationRequested) return;
                Render(cmds.Concat(data).ToList());
            });
        }
        catch (OperationCanceledException) { }
        catch { /* palette نباید بشکند */ }
    }

    private void PaletteBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.Down:
                if (ResultsList.Items.Count > 0)
                {
                    ResultsList.SelectedIndex = Math.Min(ResultsList.SelectedIndex + 1, ResultsList.Items.Count - 1);
                    ResultsList.ScrollIntoView(ResultsList.SelectedItem);
                    e.Handled = true;
                }
                break;
            case Key.Up:
                if (ResultsList.Items.Count > 0)
                {
                    ResultsList.SelectedIndex = Math.Max(ResultsList.SelectedIndex - 1, 0);
                    ResultsList.ScrollIntoView(ResultsList.SelectedItem);
                    e.Handled = true;
                }
                break;
            case Key.Enter:
                Execute(ResultsList.SelectedItem as GlobalSearchResult);
                e.Handled = true;
                break;
            case Key.Escape:
                Close();
                e.Handled = true;
                break;
        }
    }

    private void ResultsList_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) { Execute(ResultsList.SelectedItem as GlobalSearchResult); e.Handled = true; }
    }

    private void Results_Activate(object sender, MouseButtonEventArgs e)
        => Execute(ResultsList.SelectedItem as GlobalSearchResult);

    private void Execute(GlobalSearchResult? r)
    {
        if (r is null) return;
        Close();
        _onExecute(r);
    }
}
