namespace SamaHesab.Application.Reports;

/// <summary>دستهٔ جریان وجوه نقد طبق استاندارد: عملیاتی/سرمایه‌گذاری/تأمین‌مالی.</summary>
public enum CashFlowCategory { Operating, Investing, Financing }

/// <summary>
/// طبقه‌بندیِ خالصِ جریان وجوه نقد بر اساس بخش اولِ کدِ حساب (مطابق نمودار واقعی).
///   نقد و معادل‌ها = «1-01» · سرمایه‌گذاری = گروه «2» (دارایی ثابت)
///   تأمین‌مالی = گروه «4»/«5» + «3-06» (سود سهام) + «3-07» (تسهیلات کوتاه‌مدت)
///   بقیه (درآمد/هزینه/دریافتنی/پرداختنی/موجودی) = عملیاتی
/// </summary>
public static class CashFlowClassifier
{
    private static string Seg0(string? code) => (code ?? "").Split('-').FirstOrDefault() ?? "";

    /// <summary>آیا این حساب، نقد و معادل‌های نقد است؟ (کدِ «1-01...»)</summary>
    public static bool IsCash(string? code) => (code ?? "").StartsWith("1-01");

    private static bool IsFinancing(string code) =>
        Seg0(code) is "4" or "5" || code.StartsWith("3-06") || code.StartsWith("3-07");

    private static bool IsInvesting(string code) => Seg0(code) == "2";

    /// <summary>
    /// دستهٔ یک حرکتِ نقد بر اساس حساب‌های طرفِ مقابل (غیرنقد).
    /// تقدم: تأمین‌مالی > سرمایه‌گذاری > عملیاتی (دستهٔ خاص‌تر اولویت دارد).
    /// </summary>
    public static CashFlowCategory Categorize(IEnumerable<string> counterpartCodes)
    {
        var codes = counterpartCodes.Where(c => !string.IsNullOrEmpty(c)).ToList();
        if (codes.Any(IsFinancing)) return CashFlowCategory.Financing;
        if (codes.Any(IsInvesting)) return CashFlowCategory.Investing;
        return CashFlowCategory.Operating;
    }
}

/// <summary>یک حرکتِ نقد: خالصِ تغییرِ نقد در یک سند + کدِ حساب‌های طرفِ مقابل.</summary>
public record CashMovement(decimal CashDelta, IReadOnlyList<string> CounterpartCodes);

/// <summary>نتیجهٔ صورت جریان وجوه نقد در یک دوره.</summary>
public record CashFlowResult(decimal Operating, decimal Investing, decimal Financing)
{
    public decimal NetChange => Operating + Investing + Financing;
}

/// <summary>
/// موتور خالص صورت جریان وجوه نقد — منطق تست‌پذیر و مستقل از داده.
/// هر حرکتِ نقد را بر اساس طرفِ مقابل به یک دسته نسبت می‌دهد و جمعِ هر دسته را برمی‌گرداند.
/// (روش مستقیمِ ساده‌شده برای SMB.)
/// </summary>
public static class CashFlowEngine
{
    public static CashFlowResult Build(IEnumerable<CashMovement> movements)
    {
        decimal op = 0, inv = 0, fin = 0;
        foreach (var m in movements)
        {
            switch (CashFlowClassifier.Categorize(m.CounterpartCodes))
            {
                case CashFlowCategory.Investing: inv += m.CashDelta; break;
                case CashFlowCategory.Financing: fin += m.CashDelta; break;
                default: op += m.CashDelta; break;
            }
        }
        return new CashFlowResult(op, inv, fin);
    }
}
