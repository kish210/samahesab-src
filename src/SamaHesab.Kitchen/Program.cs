using System.Diagnostics;

// kitchen.exe — kitchen display (KDS) launcher. Starts the main application in
// kitchen mode (--kitchen). Deploy kitchen.exe next to SamaHesab.exe.

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
        "فایل SamaHesab.exe یافت نشد. kitchen.exe را کنار SamaHesab.exe قرار دهید.",
        "نمایشگر آشپزخانه", System.Windows.Forms.MessageBoxButtons.OK,
        System.Windows.Forms.MessageBoxIcon.Error);
    return 1;
}

Process.Start(new ProcessStartInfo(exe, "--kitchen") { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(exe)! });
return 0;
