using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Data.SqlClient;
using SamaHesab.WPF.Services;

namespace SamaHesab.WPF.Views.Shell;

/// <summary>
/// Structured dialog to configure the SQL Server connection (no raw string editing).
/// Builds/parses the connection string with SqlConnectionStringBuilder.
/// Opened from the login window's "تنظیمات" button.
/// </summary>
public class ConnectionSettingsWindow : Window
{
    private static readonly Color Bg = Color.FromRgb(0x1E, 0x29, 0x3B);
    private static readonly Color FieldBg = Color.FromRgb(0x0F, 0x17, 0x2A);
    private static readonly Color Muted = Color.FromRgb(0x9C, 0xA8, 0xB8);

    private readonly TextBox _server;
    private readonly TextBox _database;
    private readonly RadioButton _winAuth;
    private readonly RadioButton _sqlAuth;
    private readonly TextBox _user;
    private readonly PasswordBox _password;
    private readonly TextBlock _status;
    private readonly Grid _credGrid;

    public ConnectionSettingsWindow()
    {
        Title = "تنظیمات اتصال به پایگاه داده";
        Width = 560;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        FlowDirection = FlowDirection.RightToLeft;
        Background = new SolidColorBrush(Bg);
        ResizeMode = ResizeMode.NoResize;
        FontFamily = new FontFamily("Segoe UI, Tahoma");

        var root = new StackPanel { Margin = new Thickness(24) };

        root.Children.Add(Header("تنظیمات اتصال به پایگاه داده"));
        root.Children.Add(Hint("مشخصات اتصال به SQL Server را وارد کنید. " +
            "پیش‌فرض روی سرور Docker تنظیم شده است (localhost,1433)."));

        // Server
        root.Children.Add(Label("آدرس سرور (Server):"));
        _server = Field();
        root.Children.Add(_server);

        // Database
        root.Children.Add(Label("نام پایگاه داده (Database):"));
        _database = Field();
        root.Children.Add(_database);

        // Auth mode
        root.Children.Add(Label("نوع احراز هویت:"));
        var authPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
        _sqlAuth = new RadioButton
        {
            Content = "SQL Server (نام کاربری/رمز)",
            Foreground = Brushes.White, IsChecked = true, Margin = new Thickness(0, 0, 24, 0), FontSize = 13
        };
        _winAuth = new RadioButton
        {
            Content = "Windows (یکپارچه)",
            Foreground = Brushes.White, FontSize = 13
        };
        _sqlAuth.Checked += (_, _) => UpdateCredVisibility();
        _winAuth.Checked += (_, _) => UpdateCredVisibility();
        authPanel.Children.Add(_sqlAuth);
        authPanel.Children.Add(_winAuth);
        root.Children.Add(authPanel);

        // Credentials (User + Password) — hidden for Windows auth
        _credGrid = new Grid { Margin = new Thickness(0, 0, 0, 4) };
        _credGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        _credGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
        _credGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var userBox = new StackPanel();
        userBox.Children.Add(Label("نام کاربری:"));
        _user = Field();
        userBox.Children.Add(_user);
        Grid.SetColumn(userBox, 0);

        var passBox = new StackPanel();
        passBox.Children.Add(Label("رمز عبور:"));
        _password = new PasswordBox
        {
            MinHeight = 34, FontSize = 14, Padding = new Thickness(8, 6, 8, 6),
            Background = new SolidColorBrush(FieldBg), Foreground = Brushes.White,
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x33, 0x41, 0x55)),
            Margin = new Thickness(0, 0, 0, 8)
        };
        passBox.Children.Add(_password);
        Grid.SetColumn(passBox, 2);

        _credGrid.Children.Add(userBox);
        _credGrid.Children.Add(passBox);
        root.Children.Add(_credGrid);

        // Status line
        _status = new TextBlock
        {
            Margin = new Thickness(0, 6, 0, 10), FontSize = 13,
            TextWrapping = TextWrapping.Wrap, Foreground = Brushes.White
        };
        root.Children.Add(_status);

        // Buttons
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left };
        var btnTest = MakeButton("آزمایش اتصال", Color.FromRgb(0x25, 0x63, 0xEB));
        btnTest.Click += async (_, _) => await TestAsync();
        var btnSave = MakeButton("ذخیره", Color.FromRgb(0x16, 0xA3, 0x4A));
        btnSave.Click += (_, _) => Save();
        var btnDefault = MakeButton("پیش‌فرض Docker", Color.FromRgb(0x6B, 0x72, 0x80));
        btnDefault.Click += (_, _) => LoadFrom(AppSettingsStore.DefaultConnectionString);
        var btnCancel = MakeButton("انصراف", Color.FromRgb(0x4B, 0x55, 0x63));
        btnCancel.Click += (_, _) => Close();
        buttons.Children.Add(btnTest);
        buttons.Children.Add(btnSave);
        buttons.Children.Add(btnDefault);
        buttons.Children.Add(btnCancel);
        root.Children.Add(buttons);

        Content = root;

        LoadFrom(AppSettingsStore.GetConnectionString());
    }

    // ─── UI helpers ───────────────────────────────────────────────────────────
    private static TextBlock Header(string t) => new()
    {
        Text = t, Foreground = Brushes.White, FontSize = 18,
        FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 6)
    };
    private static TextBlock Hint(string t) => new()
    {
        Text = t, Foreground = new SolidColorBrush(Muted), FontSize = 12,
        Margin = new Thickness(0, 0, 0, 14), TextWrapping = TextWrapping.Wrap
    };
    private static TextBlock Label(string t) => new()
    {
        Text = t, Foreground = new SolidColorBrush(Muted), FontSize = 13, Margin = new Thickness(0, 0, 0, 4)
    };
    private static TextBox Field() => new()
    {
        MinHeight = 34, FontSize = 14, Padding = new Thickness(8, 6, 8, 6),
        Background = new SolidColorBrush(FieldBg), Foreground = Brushes.White,
        BorderBrush = new SolidColorBrush(Color.FromRgb(0x33, 0x41, 0x55)),
        Margin = new Thickness(0, 0, 0, 10)
    };
    private static Button MakeButton(string text, Color color) => new()
    {
        Content = text, Margin = new Thickness(0, 0, 10, 0), Padding = new Thickness(16, 8, 16, 8),
        FontSize = 14, Foreground = Brushes.White, Background = new SolidColorBrush(color),
        BorderThickness = new Thickness(0), Cursor = System.Windows.Input.Cursors.Hand
    };

    private void UpdateCredVisibility() =>
        _credGrid.Visibility = _sqlAuth.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;

    // ─── Parse / build connection string ──────────────────────────────────────
    private void LoadFrom(string connectionString)
    {
        try
        {
            var b = new SqlConnectionStringBuilder(connectionString);
            _server.Text = b.DataSource;
            _database.Text = string.IsNullOrEmpty(b.InitialCatalog) ? "SamaHesab" : b.InitialCatalog;
            if (b.IntegratedSecurity)
            {
                _winAuth.IsChecked = true;
            }
            else
            {
                _sqlAuth.IsChecked = true;
                _user.Text = b.UserID;
                _password.Password = b.Password;
            }
        }
        catch
        {
            _server.Text = "localhost,1433";
            _database.Text = "SamaHesab";
            _sqlAuth.IsChecked = true;
            _user.Text = "sa";
        }
        UpdateCredVisibility();
    }

    private string BuildConnectionString()
    {
        var b = new SqlConnectionStringBuilder
        {
            DataSource = _server.Text.Trim(),
            InitialCatalog = string.IsNullOrWhiteSpace(_database.Text) ? "SamaHesab" : _database.Text.Trim(),
            TrustServerCertificate = true,
            Encrypt = false,
            MultipleActiveResultSets = true
        };
        if (_winAuth.IsChecked == true)
        {
            b.IntegratedSecurity = true;
        }
        else
        {
            b.UserID = _user.Text.Trim();
            b.Password = _password.Password;
        }
        return b.ConnectionString;
    }

    private async Task TestAsync()
    {
        _status.Foreground = Brushes.Khaki;
        _status.Text = "در حال آزمایش دسترس‌پذیریِ سرور...";

        // فقط «دسترس‌پذیریِ سرور» را با آدرس/نامِ سرور بررسی می‌کنیم — نیازی به نام‌کاربری/رمز نیست.
        // به master وصل می‌شویم؛ اگر سرور پاسخ دهد (حتی با خطای احراز هویت) یعنی آدرس/نام درست است.
        var b = new SqlConnectionStringBuilder
        {
            DataSource = _server.Text.Trim(),
            InitialCatalog = "master",
            TrustServerCertificate = true,
            Encrypt = false,
            ConnectTimeout = 5
        };
        if (_winAuth.IsChecked == true || string.IsNullOrWhiteSpace(_user.Text))
        {
            b.IntegratedSecurity = true;   // بدونِ user/pass
        }
        else
        {
            b.UserID = _user.Text.Trim();
            b.Password = _password.Password;
        }

        try
        {
            await Task.Run(() =>
            {
                using var conn = new SqlConnection(b.ConnectionString);
                conn.Open();
            });
            _status.Foreground = Brushes.LightGreen;
            _status.Text = "✅ سرور در دسترس است و اتصال برقرار شد.";
        }
        catch (SqlException ex)
        {
            // خطاهای احراز هویت/دیتابیس یعنی سرور پاسخ داده ⇒ آدرس/نامِ سرور درست است.
            bool reachable = ex.Number is 18456 or 18452 or 4060 or 916 or 40615 or 233;
            if (reachable)
            {
                _status.Foreground = Brushes.LightGreen;
                _status.Text = "✅ سرور در دسترس است (آدرس/نامِ سرور درست است). " +
                               "برای ورود، نام‌کاربری و رمز را در فرمِ لاگین وارد کنید.";
            }
            else
            {
                _status.Foreground = Brushes.IndianRed;
                _status.Text = "❌ سرور در دسترس نیست (آدرس/نامِ سرور را بررسی کنید): " + ex.Message;
            }
        }
        catch (Exception ex)
        {
            _status.Foreground = Brushes.IndianRed;
            _status.Text = "❌ خطا در آزمایش: " + ex.Message;
        }
    }

    private void Save()
    {
        try
        {
            AppSettingsStore.SaveConnectionString(BuildConnectionString());
            MessageBox.Show(
                "تنظیمات ذخیره شد. برای اعمال تغییرات، برنامه را دوباره اجرا کنید.",
                "ذخیره شد", MessageBoxButton.OK, MessageBoxImage.Information);
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show("خطا در ذخیره تنظیمات: " + ex.Message,
                "خطا", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
