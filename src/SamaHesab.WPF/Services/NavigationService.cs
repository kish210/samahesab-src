using System.Windows.Controls;

namespace SamaHesab.WPF.Services;

public interface INavigationService
{
    event EventHandler<NavigationEventArgs>? Navigated;
    void NavigateTo(string viewName, object? parameter = null);
    void NavigateBack();
    bool CanNavigateBack { get; }
    string CurrentView { get; }
}

public class NavigationEventArgs : EventArgs
{
    public string ViewName { get; }
    public object? Parameter { get; }

    public NavigationEventArgs(string viewName, object? parameter = null)
    {
        ViewName = viewName;
        Parameter = parameter;
    }
}

public class NavigationService : INavigationService
{
    private readonly Stack<string> _navigationStack = new();
    public event EventHandler<NavigationEventArgs>? Navigated;

    public string CurrentView { get; private set; } = "Dashboard";
    public bool CanNavigateBack => _navigationStack.Count > 1;

    public void NavigateTo(string viewName, object? parameter = null)
    {
        _navigationStack.Push(viewName);
        CurrentView = viewName;
        Navigated?.Invoke(this, new NavigationEventArgs(viewName, parameter));
    }

    public void NavigateBack()
    {
        if (!CanNavigateBack) return;
        _navigationStack.Pop();
        var viewName = _navigationStack.Peek();
        CurrentView = viewName;
        Navigated?.Invoke(this, new NavigationEventArgs(viewName));
    }
}

public interface IDialogService
{
    Task<bool> ConfirmAsync(string message, string title = "تأیید");
    Task ShowErrorAsync(string message, string title = "خطا");
    Task ShowSuccessAsync(string message, string title = "موفق");
    Task ShowInfoAsync(string message, string title = "اطلاعات");
    Task ShowWarningAsync(string message, string title = "هشدار");
    Task<string?> ShowInputAsync(string prompt, string title = "ورود اطلاعات");
    Task<string?> PromptAsync(string message, string title = "ورودی", string defaultValue = "");
}

public class DialogService : IDialogService
{
    public Task<bool> ConfirmAsync(string message, string title = "تأیید")
    {
        var result = System.Windows.MessageBox.Show(message, title,
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Question,
            System.Windows.MessageBoxResult.No,
            System.Windows.MessageBoxOptions.RightAlign | System.Windows.MessageBoxOptions.RtlReading);

        return Task.FromResult(result == System.Windows.MessageBoxResult.Yes);
    }

    public Task ShowErrorAsync(string message, string title = "خطا")
    {
        System.Windows.MessageBox.Show(message, title,
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Error,
            System.Windows.MessageBoxResult.OK,
            System.Windows.MessageBoxOptions.RightAlign | System.Windows.MessageBoxOptions.RtlReading);
        return Task.CompletedTask;
    }

    public Task ShowSuccessAsync(string message, string title = "موفق")
    {
        System.Windows.MessageBox.Show(message, title,
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Information,
            System.Windows.MessageBoxResult.OK,
            System.Windows.MessageBoxOptions.RightAlign | System.Windows.MessageBoxOptions.RtlReading);
        return Task.CompletedTask;
    }

    public Task ShowInfoAsync(string message, string title = "اطلاعات")
    {
        System.Windows.MessageBox.Show(message, title,
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Information,
            System.Windows.MessageBoxResult.OK,
            System.Windows.MessageBoxOptions.RightAlign | System.Windows.MessageBoxOptions.RtlReading);
        return Task.CompletedTask;
    }

    public Task ShowWarningAsync(string message, string title = "هشدار")
    {
        System.Windows.MessageBox.Show(message, title,
            System.Windows.MessageBoxButton.OK,
            System.Windows.MessageBoxImage.Warning,
            System.Windows.MessageBoxResult.OK,
            System.Windows.MessageBoxOptions.RightAlign | System.Windows.MessageBoxOptions.RtlReading);
        return Task.CompletedTask;
    }

    public Task<string?> ShowInputAsync(string prompt, string title = "ورود اطلاعات")
        => PromptAsync(prompt, title);

    /// <summary>دیالوگ ورودی متن ساده (RTL) — یک TextBox با تأیید/انصراف.</summary>
    public Task<string?> PromptAsync(string message, string title = "ورودی", string defaultValue = "")
    {
        var win = new System.Windows.Window
        {
            Title = title, Width = 380, SizeToContent = System.Windows.SizeToContent.Height,
            WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen,
            FlowDirection = System.Windows.FlowDirection.RightToLeft,
            ResizeMode = System.Windows.ResizeMode.NoResize,
        };
        var root = new System.Windows.Controls.StackPanel { Margin = new System.Windows.Thickness(16) };
        root.Children.Add(new System.Windows.Controls.TextBlock
        { Text = message, Margin = new System.Windows.Thickness(0, 0, 0, 8), FontSize = 13 });
        var box = new System.Windows.Controls.TextBox
        { Text = defaultValue, FontSize = 14, Padding = new System.Windows.Thickness(6,4,6,4) };
        root.Children.Add(box);
        var btns = new System.Windows.Controls.StackPanel
        { Orientation = System.Windows.Controls.Orientation.Horizontal,
          HorizontalAlignment = System.Windows.HorizontalAlignment.Left, Margin = new System.Windows.Thickness(0,14,0,0) };
        var ok = new System.Windows.Controls.Button { Content = "تأیید", Width = 84, Height = 30, IsDefault = true, Margin = new System.Windows.Thickness(0,0,8,0) };
        var cancel = new System.Windows.Controls.Button { Content = "انصراف", Width = 84, Height = 30, IsCancel = true };
        btns.Children.Add(ok); btns.Children.Add(cancel);
        root.Children.Add(btns);
        win.Content = root;
        string? result = null;
        ok.Click += (_, _) => { result = box.Text; win.DialogResult = true; };
        box.Loaded += (_, _) => { box.SelectAll(); box.Focus(); };
        var dr = win.ShowDialogOwned();   // Owner امن (پرهیز از self-owner وقتی MainWindow null است)
        return Task.FromResult(dr == true ? result : null);
    }
}

public interface IThemeService
{
    string CurrentTheme { get; }
    void SetTheme(string theme);
    void ToggleTheme();
}

public class ThemeService : IThemeService
{
    public string CurrentTheme { get; private set; } = "Dark";

    public void SetTheme(string theme)
    {
        CurrentTheme = theme;
        var dict = new System.Windows.ResourceDictionary
        {
            Source = new Uri($"/Assets/Themes/{theme}.xaml", UriKind.Relative)
        };

        var mergedDicts = System.Windows.Application.Current.Resources.MergedDictionaries;
        mergedDicts.Clear();
        mergedDicts.Add(dict);
    }

    public void ToggleTheme()
    {
        SetTheme(CurrentTheme == "Dark" ? "Light" : "Dark");
    }
}
