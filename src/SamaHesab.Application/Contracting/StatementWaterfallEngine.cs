namespace SamaHesab.Application.Contracting;

/// <summary>ورودیِ آبشارِ محاسبهٔ صورت‌وضعیت. درصدها به‌صورتِ «۵ = ۵٪» تفسیر می‌شوند.</summary>
public record WaterfallInput(
    decimal CumulativeGrossWork, decimal PreviousCumulative,
    decimal AdjustmentAmount, decimal MaterialDiffAmount,
    decimal AdvancePercent, decimal RetentionPercent, decimal InsurancePercent, decimal TaxPercent,
    decimal Penalty, decimal Other,
    decimal AdvanceOutstanding);   // ماندهٔ بازیافت‌نشدهٔ پیش‌پرداخت (سقفِ بازیافت)

/// <summary>نتیجهٔ آبشار — همهٔ اجزای صورت‌وضعیت تفکیک‌شده.</summary>
public record WaterfallResult(
    decimal PeriodWork, decimal GrossThisPeriod,
    decimal AdvanceRecovery, decimal Retention, decimal Insurance, decimal Tax,
    decimal Penalty, decimal Other, decimal NetPayable);

/// <summary>
/// CON — موتورِ خالصِ آبشارِ صورت‌وضعیت (مستقل و تست‌پذیر، بدونِ DB).
/// کارکردِ دوره = تجمعی − قبلی؛ ناخالص = دوره + تعدیل + مابه‌التفاوت؛ کسورات = پایه × نرخ؛
/// بازیافتِ پیش‌پرداخت سقف‌دار (هرگز بیش از ماندهٔ پیش‌پرداخت). خالص = ناخالص − همهٔ کسورات.
/// (در ابتدا برای CON-C2-1 برنامه‌ریزی شده بود؛ چون بلاکرِ C1-3 بود، C1 آن را با تست ساخت.)
/// </summary>
public static class StatementWaterfallEngine
{
    public static WaterfallResult Compute(WaterfallInput i)
    {
        var periodWork = i.CumulativeGrossWork - i.PreviousCumulative;
        var gross = periodWork + i.AdjustmentAmount + i.MaterialDiffAmount;

        // بازیافتِ پیش‌پرداخت بر مبنای کارکردِ دوره، با سقفِ ماندهٔ پیش‌پرداخت.
        var advanceRaw = Pct(i.AdvancePercent) * NonNeg(periodWork);
        var advanceRecovery = Clamp(advanceRaw, 0, NonNeg(i.AdvanceOutstanding));

        var retention = Round(Pct(i.RetentionPercent) * NonNeg(gross));
        var insurance = Round(Pct(i.InsurancePercent) * NonNeg(gross));
        var tax = Round(Pct(i.TaxPercent) * NonNeg(gross));
        var penalty = NonNeg(i.Penalty);
        var other = NonNeg(i.Other);
        advanceRecovery = Round(advanceRecovery);

        var net = gross - advanceRecovery - retention - insurance - tax - penalty - other;

        return new WaterfallResult(Round(periodWork), Round(gross),
            advanceRecovery, retention, insurance, tax, penalty, other, Round(net));
    }

    private static decimal Pct(decimal percent) => percent / 100m;
    private static decimal NonNeg(decimal v) => v < 0 ? 0 : v;
    private static decimal Clamp(decimal v, decimal lo, decimal hi) => v < lo ? lo : (v > hi ? hi : v);
    private static decimal Round(decimal v) => Math.Round(v, 0, MidpointRounding.AwayFromZero);
}
