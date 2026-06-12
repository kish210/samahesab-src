using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace SamaHesab.WPF.Services;

/// <summary>
/// ماندگاری سبکِ وضعیت مغایرت‌گیری بانکی (R4) — بدون تغییر اسکیمای پایگاه‌داده.
/// به‌ازای هر حساب بانکی، شناسهٔ ردیف‌های دفترِ تطبیق‌شده و تاریخ آخرین تطبیق را
/// در %AppData%\SamaHesab\bank-recon.json نگه می‌دارد تا ردیف‌های تطبیق‌شده دوباره نمایش داده نشوند.
/// </summary>
public static class BankReconciliationStore
{
    private static string FilePath => Path.Combine(AppSettingsStore.AppDataDir, "bank-recon.json");

    public class ReconState
    {
        public string LastDate { get; set; } = "";
        public List<int> ReconciledItemIds { get; set; } = new();
    }

    // کلید = شناسهٔ حساب بانکی
    private static Dictionary<int, ReconState> Load()
    {
        try
        {
            if (File.Exists(FilePath))
                return JsonSerializer.Deserialize<Dictionary<int, ReconState>>(File.ReadAllText(FilePath))
                       ?? new();
        }
        catch { /* فایل خراب → از نو */ }
        return new();
    }

    private static void Save(Dictionary<int, ReconState> data)
    {
        Directory.CreateDirectory(AppSettingsStore.AppDataDir);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
    }

    public static ReconState Get(int bankAccountId)
    {
        var data = Load();
        return data.TryGetValue(bankAccountId, out var s) ? s : new ReconState();
    }

    /// <summary>افزودن شناسه‌های تطبیق‌شده‌ی جدید + ثبت تاریخ آخرین تطبیق.</summary>
    public static void AddReconciled(int bankAccountId, IEnumerable<int> itemIds, string date)
    {
        var data = Load();
        if (!data.TryGetValue(bankAccountId, out var s)) { s = new ReconState(); data[bankAccountId] = s; }
        s.LastDate = date;
        foreach (var id in itemIds)
            if (!s.ReconciledItemIds.Contains(id)) s.ReconciledItemIds.Add(id);
        Save(data);
    }
}
