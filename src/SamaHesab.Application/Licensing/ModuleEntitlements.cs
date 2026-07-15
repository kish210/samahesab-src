namespace SamaHesab.Application.Licensing;

/// <summary>
/// U-LIC-MODULE — نگاشتِ «کدام ماژولِ اختیاری در کدام بستهٔ لایسنس مجاز است».
/// پیش‌تر این نگاشت اصلاً وجود نداشت؛ SamaHesab.WPF.Services.ModuleService.SetEnabled فقط
/// core-lock/تداخلِ ماژول‌ها را چک می‌کرد، نه اینکه کاربر واقعاً بستهٔ آن ماژول را خریده باشد.
/// کلیدهایِ ماژول این‌جا به‌صورتِ رشتهٔ ثابت تکرار شده‌اند (این لایه نباید به WPF وابسته شود)؛
/// باید عیناً با ثابت‌هایِ هم‌نامِ ModuleService هم‌گام بمانند.
/// </summary>
public static class ModuleEntitlements
{
    public const string Pos = "POS", Restaurant = "Restaurant", Tourism = "Tourism",
        Hr = "HR", Crm = "CRM", Hotel = "Hotel", Support = "Support", Contracting = "Contracting",
        TaxInvoicing = "TaxInvoicing";

    private static readonly IReadOnlyDictionary<LicenseTier, string[]> Allowed = new Dictionary<LicenseTier, string[]>
    {
        [LicenseTier.Starter] = Array.Empty<string>(),
        [LicenseTier.Professional] = new[] { Pos, Restaurant, Crm },
        [LicenseTier.Enterprise] = new[] { Pos, Restaurant, Tourism, Hr, Crm, Hotel, Support, Contracting, TaxInvoicing },
    };

    /// <summary>
    /// آیا ماژولِ اختیاریِ <paramref name="moduleKey"/> در بستهٔ <paramref name="tier"/> مجاز است؟
    /// Trial همیشه مجاز است — محدودیتِ نسخهٔ آزمایشی از زمان/تعدادِ سند می‌آید، نه فهرستِ ماژول‌ها.
    /// </summary>
    public static bool IsAllowed(LicenseTier tier, string moduleKey)
        => tier == LicenseTier.Trial || (Allowed.TryGetValue(tier, out var keys) && keys.Contains(moduleKey));
}
