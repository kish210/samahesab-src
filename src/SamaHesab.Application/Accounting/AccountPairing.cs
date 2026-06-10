namespace SamaHesab.Application.Accounting;

/// <summary>
/// پیشنهاد هوشمند حساب طرفِ مقابل برای ورود سریع سند (Smart Account Suggestions).
/// منطق خالص و تست‌پذیر: از روی تاریخچه‌ی اسناد، حساب‌هایی که بیشترین‌بار همراه یک حساب
/// در یک سند آمده‌اند را به‌ترتیب فراوانی برمی‌گرداند تا کاربر ردیف بعد را با یک کلید بپذیرد.
/// </summary>
public static class AccountPairing
{
    public record Suggestion(int AccountId, int Count);

    /// <param name="vouchers">برای هر سند، مجموعه‌ی شناسه‌ی حساب‌های آن سند.</param>
    /// <param name="forAccountId">حسابی که کاربر همین حالا انتخاب کرده.</param>
    public static List<Suggestion> Suggest(
        IEnumerable<IReadOnlyCollection<int>> vouchers, int forAccountId, int top = 6)
    {
        var counts = new Dictionary<int, int>();
        foreach (var accountIds in vouchers)
        {
            if (!accountIds.Contains(forAccountId)) continue;
            foreach (var other in accountIds)
            {
                if (other == forAccountId) continue;
                counts[other] = counts.GetValueOrDefault(other) + 1;
            }
        }
        return counts
            .OrderByDescending(kv => kv.Value)
            .ThenBy(kv => kv.Key)
            .Take(top)
            .Select(kv => new Suggestion(kv.Key, kv.Value))
            .ToList();
    }
}
