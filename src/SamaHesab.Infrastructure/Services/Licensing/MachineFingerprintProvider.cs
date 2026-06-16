using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;
using SamaHesab.Application.Licensing;

namespace SamaHesab.Infrastructure.Services.Licensing;

/// <summary>
/// فاز ۱۲ P-G7 — اثرِانگشتِ سخت‌افزاریِ دستگاه: ترکیبِ شناسهٔ مادربرد + Windows MachineGuid + شناسهٔ CPU
/// (نه فقط MAC). نتیجه = SHA-256 (Hex، ۳۲ نویسهٔ اول). در برابرِ نبودِ یک منبع، graceful است.
/// </summary>
public sealed class MachineFingerprintProvider : IMachineFingerprintProvider
{
    public string GetFingerprint()
    {
        var parts = new[]
        {
            Safe(() => Wmi("Win32_BaseBoard", "SerialNumber")),   // مادربرد
            Safe(() => Wmi("Win32_Processor", "ProcessorId")),    // CPU
            Safe(MachineGuid),                                    // Windows MachineGuid
        };
        var seed = string.Join("|", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
        if (string.IsNullOrWhiteSpace(seed)) seed = Environment.MachineName;  // fallback

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(seed.ToUpperInvariant()));
        return Convert.ToHexString(hash)[..32];
    }

    private static string Safe(Func<string?> f) { try { return f() ?? ""; } catch { return ""; } }

    private static string? MachineGuid()
    {
        using var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
            .OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
        return key?.GetValue("MachineGuid") as string;
    }

    private static string? Wmi(string wmiClass, string property)
    {
#pragma warning disable CA1416 // فقط ویندوز — کلاینت ویندوزی است
        using var searcher = new System.Management.ManagementObjectSearcher($"SELECT {property} FROM {wmiClass}");
        foreach (var o in searcher.Get())
        {
            var v = o[property]?.ToString()?.Trim();
            if (!string.IsNullOrWhiteSpace(v)) return v;
        }
        return null;
#pragma warning restore CA1416
    }
}
