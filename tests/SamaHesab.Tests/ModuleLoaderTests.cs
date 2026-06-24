using System.Collections.Generic;
using System.IO;
using System.Linq;
using SamaHesab.Infrastructure.Modules;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>فاز۴ — تستِ ModuleLoader: کشف/بارگذاریِ IModule از DLL + dedupe + ایمنیِ پوشهٔ نبود.</summary>
public class ModuleLoaderTests
{
    private static string TempDir()
    {
        var d = Path.Combine(Path.GetTempPath(), "samaloader_" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(d);
        return d;
    }

    [Fact]
    public void Missing_Directory_Returns_Empty()
    {
        var res = ModuleLoader.LoadFromDirectory(Path.Combine(Path.GetTempPath(), "no_such_" + System.Guid.NewGuid().ToString("N")),
            new HashSet<string>());
        Assert.Empty(res);
    }

    [Fact]
    public void Loads_Module_Dll_And_Discovers_IModule()
    {
        var dir = TempDir();
        // DLLِ واقعیِ ماژولِ پیمانکاری را در پوشه کپی می‌کنیم (نامِ SamaHesab.Modules.*.dll).
        var srcDll = typeof(SamaHesab.Modules.Contracting.ContractingModule).Assembly.Location;
        File.Copy(srcDll, Path.Combine(dir, Path.GetFileName(srcDll)), overwrite: true);

        var loaded = ModuleLoader.LoadFromDirectory(dir, new HashSet<string>());
        Assert.Contains(loaded, m => m.Key == "Contracting");
    }

    [Fact]
    public void Skips_Module_Already_Loaded_By_Key()
    {
        var dir = TempDir();
        var srcDll = typeof(SamaHesab.Modules.Contracting.ContractingModule).Assembly.Location;
        File.Copy(srcDll, Path.Combine(dir, Path.GetFileName(srcDll)), overwrite: true);

        // کلیدِ Contracting از قبل (bundle) موجود است → باید رد شود.
        var loaded = ModuleLoader.LoadFromDirectory(dir, new HashSet<string> { "Contracting" });
        Assert.DoesNotContain(loaded, m => m.Key == "Contracting");
    }
}
