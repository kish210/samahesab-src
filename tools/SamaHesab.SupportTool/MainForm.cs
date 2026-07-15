using SamaHesab.Application.Licensing;

namespace SamaHesab.SupportTool;

/// <summary>
/// U-SUPPORT-RESET — ابزارِ گرافیکیِ WinForms برایِ صدورِ کدِ ریستِ پشتیبانی (به‌جایِ نسخهٔ
/// کنسولی/WPFِ قبلی، به‌درخواستِ کاربر). فقط دستِ پشتیبانی/وندور اجرا می‌شود؛ کلیدِ خصوصیِ RSA از
/// یک فایلِ PEM محلی (خارج از این پروژه/گیت) انتخاب می‌شود، هرگز در کد نیست.
/// </summary>
public sealed class MainForm : Form
{
    private readonly TextBox _txtFingerprint = new();
    private readonly TextBox _txtKeyPath = new() { ReadOnly = true };
    private readonly NumericUpDown _numDays = new() { Minimum = 1, Maximum = 365, Value = 2, Width = 70 };
    private readonly TextBox _txtResultCode = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Height = 100 };
    private readonly Label _lblResultInfo = new() { AutoSize = false, Height = 40, ForeColor = Color.DarkGreen };
    private readonly Label _lblError = new() { AutoSize = false, Height = 36, ForeColor = Color.Firebrick, Visible = false };
    private readonly Button _btnCopy = new() { Text = "📋 کپی در کلیپ‌بورد", Height = 32, Width = 420, Visible = false };

    public MainForm()
    {
        Text = "ابزارِ پشتیبانیِ سما حساب — صدورِ کدِ ریست";
        Font = new Font("Segoe UI", 9.5f);
        RightToLeft = RightToLeft.Yes;
        RightToLeftLayout = true;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(460, 480);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            ColumnCount = 1,
            AutoSize = true,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var title = new Label
        {
            Text = "🔧 صدورِ کدِ ریستِ پشتیبانی",
            Font = new Font("Segoe UI", 13f, FontStyle.Bold),
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 4),
        };
        var subtitle = new Label
        {
            Text = "کدِ دستگاهِ مشتری را بگیرید، کلیدِ خصوصیِ RSA (همان کلیدِ لایسنس) را انتخاب کنید، و کدِ ریست را برایِ مشتری بفرستید.",
            AutoSize = false,
            Height = 45,
            Width = 420,
        };

        var lblFp = new Label { Text = "کدِ دستگاهِ مشتری (Fingerprint):", AutoSize = true, Margin = new Padding(0, 10, 0, 3) };
        _txtFingerprint.Dock = DockStyle.Fill;
        _txtFingerprint.RightToLeft = RightToLeft.No;

        var lblKey = new Label { Text = "فایلِ کلیدِ خصوصیِ RSA (PEM):", AutoSize = true, Margin = new Padding(0, 10, 0, 3) };
        var keyRow = new TableLayoutPanel { ColumnCount = 2, Dock = DockStyle.Fill, Height = 28 };
        keyRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        keyRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        _txtKeyPath.Dock = DockStyle.Fill;
        _txtKeyPath.RightToLeft = RightToLeft.No;
        var btnBrowse = new Button { Text = "انتخاب…", Dock = DockStyle.Fill };
        btnBrowse.Click += BrowseKey_Click;
        keyRow.Controls.Add(_txtKeyPath, 0, 0);
        keyRow.Controls.Add(btnBrowse, 1, 0);

        var lblDays = new Label { Text = "مدتِ اعتبارِ کد (روز):", AutoSize = true, Margin = new Padding(0, 10, 0, 3) };

        var btnGenerate = new Button
        {
            Text = "ساختِ کدِ ریست",
            Height = 36,
            Width = 420,
            Margin = new Padding(0, 16, 0, 0),
            BackColor = Color.FromArgb(50, 74, 122),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
        };
        btnGenerate.Click += Generate_Click;

        _txtResultCode.RightToLeft = RightToLeft.No;
        _txtResultCode.Dock = DockStyle.Fill;
        _txtResultCode.Visible = false;
        _lblResultInfo.Visible = false;

        _btnCopy.Click += (_, _) => { if (_txtResultCode.Text.Length > 0) Clipboard.SetText(_txtResultCode.Text); };

        _lblError.Width = 420;

        layout.Controls.Add(title);
        layout.Controls.Add(subtitle);
        layout.Controls.Add(_lblError);
        layout.Controls.Add(lblFp);
        layout.Controls.Add(_txtFingerprint);
        layout.Controls.Add(lblKey);
        layout.Controls.Add(keyRow);
        layout.Controls.Add(lblDays);
        layout.Controls.Add(_numDays);
        layout.Controls.Add(btnGenerate);
        layout.Controls.Add(_lblResultInfo);
        layout.Controls.Add(_txtResultCode);
        layout.Controls.Add(_btnCopy);

        Controls.Add(layout);
    }

    private void BrowseKey_Click(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog { Filter = "کلیدِ خصوصیِ PEM (*.pem;*.key;*.txt)|*.pem;*.key;*.txt|همهٔ فایل‌ها (*.*)|*.*" };
        if (dlg.ShowDialog(this) == DialogResult.OK) _txtKeyPath.Text = dlg.FileName;
    }

    private void Generate_Click(object? sender, EventArgs e)
    {
        _lblError.Visible = false;
        _lblResultInfo.Visible = false;
        _txtResultCode.Visible = false;
        _btnCopy.Visible = false;

        var fingerprint = _txtFingerprint.Text.Trim();
        var keyPath = _txtKeyPath.Text.Trim();

        if (string.IsNullOrWhiteSpace(fingerprint))
        { ShowError("کدِ دستگاهِ مشتری را وارد کنید."); return; }
        if (string.IsNullOrWhiteSpace(keyPath) || !File.Exists(keyPath))
        { ShowError("فایلِ کلیدِ خصوصی را انتخاب کنید (یافت نشد)."); return; }

        try
        {
            var privateKeyPem = File.ReadAllText(keyPath);
            var now = DateTime.UtcNow;
            var token = new SupportResetToken(fingerprint, now, now.AddDays((double)_numDays.Value));
            var signature = SupportResetTokenSigner.Sign(token, privateKeyPem);
            var code = new SupportResetTokenDocument(token, signature).ToCode();

            _txtResultCode.Text = code;
            _lblResultInfo.Text = $"معتبر تا {token.ExpiresUtc:yyyy-MM-dd HH:mm} UTC — فقط برایِ همین دستگاه. این متن را عیناً (کپی/پیست) برایِ مشتری بفرستید.";
            _lblResultInfo.Visible = true;
            _txtResultCode.Visible = true;
            _btnCopy.Visible = true;
        }
        catch (Exception ex)
        {
            ShowError("خطا در ساختِ کد — فایلِ کلید معتبر نیست؟ " + ex.Message);
        }
    }

    private void ShowError(string message)
    {
        _lblError.Text = message;
        _lblError.Visible = true;
    }
}
