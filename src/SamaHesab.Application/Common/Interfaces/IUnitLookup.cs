namespace SamaHesab.Application.Common.Interfaces;

/// <summary>
/// فاز ۱۲ G4.2 — جست‌وجوی واحدِ اندازه‌گیری (Cfg.Units بدونِ entityِ EF نگاشته‌شده است).
/// برای ورودِ اکسلِ کالا: نگاشتِ نامِ واحد → شناسه + واحدِ پیش‌فرض.
/// </summary>
public interface IUnitLookup
{
    /// <summary>همهٔ واحدها: نام → شناسه (case-insensitive).</summary>
    IReadOnlyDictionary<string, int> All();

    /// <summary>شناسهٔ واحد بر اساسِ نام؛ نبود → null.</summary>
    int? Resolve(string? name);

    /// <summary>واحدِ پیش‌فرض (عدد، یا کوچک‌ترین شناسه)؛ نبودِ هیچ واحد → null.</summary>
    int? DefaultUnitId();
}
