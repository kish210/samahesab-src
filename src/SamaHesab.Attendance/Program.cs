using System.Diagnostics;

// hozur.exe — لانچرِ اپلیکیشنِ مستقلِ «حضور و غیاب». برنامهٔ اصلی را با پرچمِ
// --attendance اجرا می‌کند تا کارگاهِ تخصصیِ تردد (AttendanceWorkspaceWindow) باز شود.
// کنارِ SamaHesab.exe مستقر می‌شود.

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
        "فایل SamaHesab.exe یافت نشد. hozur.exe را کنار SamaHesab.exe قرار دهید.",
        "حضور و غیاب", System.Windows.Forms.MessageBoxButtons.OK,
        System.Windows.Forms.MessageBoxIcon.Error);
    return 1;
}

Process.Start(new ProcessStartInfo(exe, "--attendance") { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(exe)! });
return 0;
