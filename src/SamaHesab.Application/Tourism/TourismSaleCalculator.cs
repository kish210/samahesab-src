namespace SamaHesab.Application.Tourism;

/// <summary>یک خطِ فروشِ گردشگری (ورودیِ خالص برای محاسبه).</summary>
public record TourismSaleLineInput(
    decimal Quantity,
    decimal UnitPrice,        // فیِ فروش به مشتری
    decimal UnitCost,         // بهای تمام‌شده (برداشت از ودیعهٔ تأمین‌کننده)
    decimal DiscountAmount = 0);

/// <summary>نتیجهٔ محاسبهٔ یک خط — برای سند و گزارش.</summary>
public record TourismSaleLineResult(
    decimal Gross,            // ناخالص = تعداد × فی
    decimal Discount,
    decimal NetSale,          // خالص = ناخالص − تخفیف
    decimal Cost,             // بهای تمام‌شده = تعداد × فیِ خرید (برداشتِ ودیعه)
    decimal Profit);          // سود = خالص − بهای تمام‌شده

/// <summary>سرجمعِ یک فروش.</summary>
public record TourismSaleTotals(
    decimal Gross, decimal Discount, decimal NetSale,
    decimal Cost,             // = جمعِ برداشت از ودیعه
    decimal Profit,
    decimal DepositDrawdown); // برابرِ Cost (برای وضوح در سندِ ودیعه)

/// <summary>
/// موتورِ خالصِ محاسبهٔ فروشِ گردشگری — مستقل و تست‌پذیر، بدونِ وابستگی به DB.
/// خروجی برای سندِ متوازن (COGS ↔ ودیعهٔ کنترلی) و گزارش‌های سود استفاده می‌شود.
/// </summary>
public static class TourismSaleCalculator
{
    public static TourismSaleLineResult ComputeLine(TourismSaleLineInput line)
    {
        var qty = Math.Max(0, line.Quantity);
        var gross = Round(qty * line.UnitPrice);
        var discount = Round(Math.Max(0, line.DiscountAmount));
        var netSale = gross - discount;
        var cost = Round(qty * line.UnitCost);
        var profit = netSale - cost;
        return new TourismSaleLineResult(gross, discount, netSale, cost, profit);
    }

    public static TourismSaleTotals ComputeTotals(IEnumerable<TourismSaleLineInput> lines)
    {
        decimal gross = 0, discount = 0, net = 0, cost = 0, profit = 0;
        foreach (var l in lines ?? Enumerable.Empty<TourismSaleLineInput>())
        {
            var r = ComputeLine(l);
            gross += r.Gross; discount += r.Discount; net += r.NetSale;
            cost += r.Cost; profit += r.Profit;
        }
        return new TourismSaleTotals(gross, discount, net, cost, profit, DepositDrawdown: cost);
    }

    /// <summary>ماندهٔ ودیعهٔ تأمین‌کننده پس از یک برداشت = شارژها − برداشت‌ها.</summary>
    public static decimal DepositBalance(decimal totalCharged, decimal totalDrawn) =>
        Round(totalCharged - totalDrawn);

    /// <summary>آیا ماندهٔ ودیعه برای یک برداشتِ جدید کافی است؟</summary>
    public static bool HasSufficientDeposit(decimal balance, decimal drawdown) =>
        drawdown <= balance;

    private static decimal Round(decimal v) => Math.Round(v, 0, MidpointRounding.AwayFromZero);
}
