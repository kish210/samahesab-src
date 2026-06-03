using System.Diagnostics;

// pos.exe — touch / kiosk launcher. Starts the main application directly in
// fast point-of-sale mode (fullscreen fast checkout) by passing --pos.
// Deploy pos.exe next to SamaHesab.exe.

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
        "فایل SamaHesab.exe یافت نشد. pos.exe را کنار SamaHesab.exe قرار دهید.",
        "صندوق فروش", System.Windows.Forms.MessageBoxButtons.OK,
        System.Windows.Forms.MessageBoxIcon.Error);
    return 1;
}

Process.Start(new ProcessStartInfo(exe, "--pos") { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(exe)! });
return 0;
