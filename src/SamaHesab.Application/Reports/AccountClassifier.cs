namespace SamaHesab.Application.Reports;

public enum AccountCategory { Asset, Liability, Equity, Revenue, Expense, Unknown }

/// <summary>
/// کار #۲۶ — طبقه‌بندی حساب بر اساس بخش اولِ کد، مطابقِ نمودار حساب‌های واقعیِ سیستم
/// (database/07_DefaultChartOfAccounts.sql). منطق خالص و تست‌پذیر؛ هم در سود/زیان و هم ترازنامه استفاده می‌شود.
///   1،2 = دارایی · 3،4 = بدهی · 5 = حقوق صاحبان سهام · 6 = درآمد · 7،8،9 = هزینه
/// (قبلاً اشتباهاً 4=درآمد و 5=هزینه گرفته می‌شد.)
/// </summary>
public static class AccountClassifier
{
    public static string Seg0(string? code) => (code ?? "").Split('-').FirstOrDefault() ?? "";

    public static AccountCategory Classify(string? code) => Seg0(code) switch
    {
        "1" or "2" => AccountCategory.Asset,
        "3" or "4" => AccountCategory.Liability,
        "5"        => AccountCategory.Equity,
        "6"        => AccountCategory.Revenue,
        "7" or "8" or "9" => AccountCategory.Expense,
        _ => AccountCategory.Unknown
    };
}
