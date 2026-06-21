using System.Diagnostics;

// mohajerat.exe — لانچرِ GUIِ ابزارِ مهاجرت/ورودِ داده.
// SamaHesab.exe را با پرچمِ --import اجرا می‌کند تا صفحهٔ گرافیکیِ «ورودِ داده از اکسل»
// (حساب‌فا/سپیدار/هلو/اکسلِ استاندارد) باز شود. کنارِ SamaHesab.exe نصب شود.

static string? FindMainExe()
{
    var dir = AppContext.BaseDirectory;
    var candidates = new[]
    {
        Path.Combine(dir, "SamaHesab.exe"),
        Path.Combine(dir, "..", "..", "..", "..", "SamaHesab.WPF", "bin", "Release", "net9.0-windows", "SamaHesab.exe"),
        Path.Combine(dir, "..", "..", "..", "..", "SamaHesab.WPF", "bin", "Debug", "net9.0-windows", "SamaHesab.exe"),
    };
    return candidates.Select(Path.GetFullPath).FirstOrDefault(File.Exists);
}

var exe = FindMainExe();
if (exe == null)
{
    System.Windows.Forms.MessageBox.Show(
        "فایل SamaHesab.exe یافت نشد. mohajerat.exe را کنار SamaHesab.exe قرار دهید.",
        "ابزارِ مهاجرت", System.Windows.Forms.MessageBoxButtons.OK,
        System.Windows.Forms.MessageBoxIcon.Error);
    return 1;
}

Process.Start(new ProcessStartInfo(exe, "--import") { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(exe)! });
return 0;
