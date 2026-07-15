using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace SamaHesab.WPF.Behaviors;

/// <summary>
/// U-UX-3 — ورودِ سریعِ کیبوردی در گریدِ اقلامِ فاکتور: وقتی کاربر روی آخرین ستونِ
/// دادهٔ آخرین ردیف Enter می‌زند، به‌جایِ بی‌اثر ماندن (رفتارِ پیش‌فرضِ DataGrid با
/// CanUserAddRows="False")، یک ردیفِ خالیِ نو اضافه می‌شود و فوکوس به کمبویِ کالایِ
/// همان ردیف می‌رود — تا ثبتِ فاکتور بدونِ کلیکِ ماوس یا فشردنِ F7 ادامه پیدا کند.
/// </summary>
public static class DataGridQuickEntryHelper
{
    public static void EnableEnterToAddRow(DataGrid grid, Func<ICommand?> resolveAddRowCommand,
        int productColumnIndex, int lastEditableColumnIndex)
    {
        grid.PreviewKeyDown += (_, e) =>
        {
            if (e.Key != Key.Enter) return;
            if (grid.CurrentColumn == null || grid.CurrentItem == null) return;
            if (grid.CurrentColumn.DisplayIndex != lastEditableColumnIndex) return;
            if (grid.Items.Count == 0 || grid.Items.IndexOf(grid.CurrentItem) != grid.Items.Count - 1) return;

            var addRowCommand = resolveAddRowCommand();
            if (addRowCommand == null || !addRowCommand.CanExecute(null)) return;

            e.Handled = true;
            grid.CommitEdit(DataGridEditingUnit.Cell, true);
            grid.CommitEdit(DataGridEditingUnit.Row, true);
            addRowCommand.Execute(null);

            grid.Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
            {
                if (grid.Items.Count == 0 || productColumnIndex >= grid.Columns.Count) return;
                var newItem = grid.Items[^1];
                var column = grid.Columns[productColumnIndex];
                grid.SelectedItem = newItem;
                grid.ScrollIntoView(newItem, column);
                grid.CurrentCell = new DataGridCellInfo(newItem, column);
                grid.BeginEdit();
                if (column.GetCellContent(newItem) is FrameworkElement cell &&
                    FindVisualChild<ComboBox>(cell) is ComboBox combo)
                    combo.Focus();
            });
        };
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T match) return match;
            var found = FindVisualChild<T>(child);
            if (found != null) return found;
        }
        return null;
    }
}
