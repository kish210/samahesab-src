using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using SamaHesab.Modules.Abstractions;

namespace SamaHesab.Infrastructure.Modules;

/// <summary>
/// فاز۴ — بارگذاریِ runtimeِ ماژول‌های دانلودشده (بدونِ rebuildِ هسته).
/// پوشهٔ ماژول‌ها (`%AppData%/SamaHesab/modules`) را اسکن می‌کند: هر `.mspkg` (zip) را اکسترکت،
/// سپس `SamaHesab.Modules.*.dll` را با AssemblyLoadContext.Default بار می‌کند (وابستگی‌های هسته
/// از اسمبلی‌های ازقبل‌بارگذاری‌شدهٔ میزبان resolve می‌شوند)، پیاده‌سازی‌های IModule را کشف و نمونه می‌سازد.
/// ماژولی که کلیدش از قبل (bundle یا بارگذاری‌شده) موجود است نادیده گرفته می‌شود (dedupe).
/// </summary>
public static class ModuleLoader
{
    /// <summary>
    /// ماژول‌های پوشه را بار و برمی‌گرداند. <paramref name="alreadyLoadedKeys"/> کلیدِ ماژول‌های bundle است
    /// (به‌روزرسانی می‌شود). خطاها لاگ و رد می‌شوند تا یک ماژولِ خراب کلِ بارگذاری را نشکند.
    /// </summary>
    public static IReadOnlyList<IModule> LoadFromDirectory(
        string modulesDir, ISet<string> alreadyLoadedKeys, Action<string>? log = null)
    {
        var result = new List<IModule>();
        if (string.IsNullOrWhiteSpace(modulesDir) || !Directory.Exists(modulesDir)) return result;

        // ۱) اکسترکتِ بسته‌های .mspkg (zip) به زیرپوشه (اگر تازه‌تر از پوشهٔ اکسترکت‌شده باشد).
        foreach (var pkg in SafeFiles(modulesDir, "*.mspkg"))
        {
            try
            {
                var dest = Path.Combine(modulesDir, Path.GetFileNameWithoutExtension(pkg));
                if (!Directory.Exists(dest) || File.GetLastWriteTimeUtc(pkg) > Directory.GetLastWriteTimeUtc(dest))
                {
                    if (Directory.Exists(dest)) Directory.Delete(dest, recursive: true);
                    ZipFile.ExtractToDirectory(pkg, dest);
                    log?.Invoke($"[module] extracted {Path.GetFileName(pkg)}");
                }
            }
            catch (Exception ex) { log?.Invoke($"[module] extract failed {Path.GetFileName(pkg)}: {ex.Message}"); }
        }

        // ۲) بارگذاری و کشفِ IModule از DLLهای ماژول.
        foreach (var dll in SafeFiles(modulesDir, "SamaHesab.Modules.*.dll", recursive: true))
        {
            try
            {
                var asm = AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.GetFullPath(dll));
                foreach (var type in asm.GetTypes().Where(IsLoadableModule))
                {
                    var module = (IModule)Activator.CreateInstance(type)!;
                    if (!alreadyLoadedKeys.Add(module.Key)) continue;   // قبلاً bundle/بارگذاری شده → رد
                    result.Add(module);
                    log?.Invoke($"[module] loaded {module.Key} v{module.Version} از {Path.GetFileName(dll)}");
                }
            }
            catch (Exception ex) { log?.Invoke($"[module] load failed {Path.GetFileName(dll)}: {ex.GetBaseException().Message}"); }
        }
        return result;
    }

    private static bool IsLoadableModule(Type t)
        => typeof(IModule).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false }
           && t.GetConstructor(Type.EmptyTypes) != null;

    private static IEnumerable<string> SafeFiles(string dir, string pattern, bool recursive = false)
    {
        try { return Directory.GetFiles(dir, pattern, recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly); }
        catch { return Enumerable.Empty<string>(); }
    }
}
