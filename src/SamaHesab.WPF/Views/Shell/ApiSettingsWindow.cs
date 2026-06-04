using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SamaHesab.WPF.Services;

namespace SamaHesab.WPF.Views.Shell;

/// <summary>
/// Connection settings for the POS / restaurant kiosk clients: the address of the
/// central Web API server (HTTP) plus the kiosk credentials. These apps talk to the
/// API, not the database, so on a separate machine you only set the server URL here.
/// </summary>
public class ApiSettingsWindow : Window
{
    private static readonly Color Bg = Color.FromRgb(0x1E, 0x29, 0x3B);
    private static readonly Color FieldBg = Color.FromRgb(0x0F, 0x17, 0x2A);
    private static readonly Color Muted = Color.FromRgb(0x9C, 0xA8, 0xB8);

    private readonly TextBox _url;
    private readonly TextBox _user;
    private readonly PasswordBox _password;
    private readonly TextBox _customerId;
    private readonly TextBox _warehouseId;
    private readonly TextBlock _status;

    public ApiSettingsWindow()
    {
        Title = "تنظیمات اتصال به سرور (API)";
        Width = 560; SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        FlowDirection = FlowDirection.RightToLeft;
        Background = new SolidColorBrush(Bg);
        ResizeMode = ResizeMode.NoResize;
        FontFamily = new FontFamily("Tahoma");

        var root = new StackPanel { Margin = new Thickness(24) };
        root.Children.Add(Header("اتصال به سرور سما حساب"));
        root.Children.Add(Hint("این برنامه (صندوق/رستوران) از طریق وب‌سرویس (API) به سرور مرکزی متصل می‌شود. " +
            "آدرس سرور را به‌صورت http://آی‌پی‌سرور:پورت وارد کنید."));

        root.Children.Add(Label("آدرس سرور API:"));
        _url = Field("http://192.168.1.10:5080"); root.Children.Add(_url);

        var creds = new Grid();
        creds.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        creds.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        creds.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var ub = new StackPanel(); ub.Children.Add(Label("نام کاربری:")); _user = Field("admin"); ub.Children.Add(_user); Grid.SetColumn(ub, 0);
        var pb = new StackPanel(); pb.Children.Add(Label("رمز عبور:"));
        _password = new PasswordBox { MinHeight = 34, FontSize = 14, Padding = new Thickness(8, 6, 8, 6),
            Background = new SolidColorBrush(FieldBg), Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x33, 0x41, 0x55)), Margin = new Thickness(0, 0, 0, 8) };
        pb.Children.Add(_password); Grid.SetColumn(pb, 2);
        creds.Children.Add(ub); creds.Children.Add(pb);
        root.Children.Add(creds);

        var ids = new Grid();
        ids.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        ids.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        ids.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var cb = new StackPanel(); cb.Children.Add(Label("کد مشتری پیش‌فرض:")); _customerId = Field("1"); cb.Children.Add(_customerId); Grid.SetColumn(cb, 0);
        var wb = new StackPanel(); wb.Children.Add(Label("کد انبار پیش‌فرض:")); _warehouseId = Field("1"); wb.Children.Add(_warehouseId); Grid.SetColumn(wb, 2);
        ids.Children.Add(cb); ids.Children.Add(wb);
        root.Children.Add(ids);

        _status = new TextBlock { Margin = new Thickness(0, 6, 0, 10), FontSize = 13, TextWrapping = TextWrapping.Wrap, Foreground = Brushes.White };
        root.Children.Add(_status);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left };
        var btnTest = MakeButton("آزمایش اتصال", Color.FromRgb(0x25, 0x63, 0xEB)); btnTest.Click += async (_, _) => await TestAsync();
        var btnSave = MakeButton("ذخیره", Color.FromRgb(0x16, 0xA3, 0x4A)); btnSave.Click += (_, _) => Save();
        var btnCancel = MakeButton("انصراف", Color.FromRgb(0x4B, 0x55, 0x63)); btnCancel.Click += (_, _) => Close();
        buttons.Children.Add(btnTest); buttons.Children.Add(btnSave); buttons.Children.Add(btnCancel);
        root.Children.Add(buttons);

        Content = root;
        LoadFrom(AppSettingsStore.GetApiSettings());
    }

    private void LoadFrom(ApiSettings s)
    {
        _url.Text = s.BaseUrl; _user.Text = s.Username; _password.Password = s.Password;
        _customerId.Text = s.CustomerId.ToString(); _warehouseId.Text = s.WarehouseId.ToString();
    }

    private ApiSettings Build() => new()
    {
        BaseUrl = _url.Text.Trim(),
        Username = _user.Text.Trim(),
        Password = _password.Password,
        CustomerId = int.TryParse(_customerId.Text, out var c) ? c : 1,
        WarehouseId = int.TryParse(_warehouseId.Text, out var w) ? w : 1
    };

    private async Task TestAsync()
    {
        _status.Foreground = Brushes.Khaki; _status.Text = "در حال آزمایش اتصال به سرور...";
        var s = Build();
        var client = new ApiClient(); client.Configure(s.BaseUrl);
        var (ok, error) = await client.LoginAsync(s.Username, s.Password);
        _status.Foreground = ok ? Brushes.LightGreen : Brushes.IndianRed;
        _status.Text = ok ? "✅ اتصال و ورود با موفقیت انجام شد." : "❌ خطا: " + error;
    }

    private void Save()
    {
        AppSettingsStore.SaveApiSettings(Build());
        MessageBox.Show("تنظیمات ذخیره شد.", "ذخیره", MessageBoxButton.OK, MessageBoxImage.Information);
        Close();
    }

    private static TextBlock Header(string t) => new() { Text = t, Foreground = Brushes.White, FontSize = 18, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 6) };
    private static TextBlock Hint(string t) => new() { Text = t, Foreground = new SolidColorBrush(Muted), FontSize = 12, Margin = new Thickness(0, 0, 0, 14), TextWrapping = TextWrapping.Wrap };
    private static TextBlock Label(string t) => new() { Text = t, Foreground = new SolidColorBrush(Muted), FontSize = 13, Margin = new Thickness(0, 0, 0, 4) };
    private static TextBox Field(string ph = "") => new() { Text = ph, MinHeight = 34, FontSize = 14, Padding = new Thickness(8, 6, 8, 6), Background = new SolidColorBrush(FieldBg), Foreground = Brushes.White, BorderBrush = new SolidColorBrush(Color.FromRgb(0x33, 0x41, 0x55)), Margin = new Thickness(0, 0, 0, 10) };
    private static Button MakeButton(string text, Color color) => new() { Content = text, Margin = new Thickness(0, 0, 10, 0), Padding = new Thickness(16, 8, 16, 8), FontSize = 14, Foreground = Brushes.White, Background = new SolidColorBrush(color), BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand };
}
