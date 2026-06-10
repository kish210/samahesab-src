using System.Diagnostics;

// waiter.exe — touch waiter launcher. Starts the main application in waiter mode
// (--waiter). Deploy waiter.exe next to SamaHesab.exe.

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
        "فایل SamaHesab.exe یافت نشد. waiter.exe را کنار SamaHesab.exe قرار دهید.",
        "صندوق گارسون", System.Windows.Forms.MessageBoxButtons.OK,
        System.Windows.Forms.MessageBoxIcon.Error);
    return 1;
}

Process.Start(new ProcessStartInfo(exe, "--waiter") { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(exe)! });
return 0;
