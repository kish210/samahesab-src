using System.Diagnostics;

// warehouse.exe — warehouse client launcher. Starts the main application in
// warehouse mode (--warehouse). Deploy warehouse.exe next to SamaHesab.exe.

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
        "فایل SamaHesab.exe یافت نشد. warehouse.exe را کنار SamaHesab.exe قرار دهید.",
        "انبارداری", System.Windows.Forms.MessageBoxButtons.OK,
        System.Windows.Forms.MessageBoxIcon.Error);
    return 1;
}

Process.Start(new ProcessStartInfo(exe, "--warehouse") { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(exe)! });
return 0;
