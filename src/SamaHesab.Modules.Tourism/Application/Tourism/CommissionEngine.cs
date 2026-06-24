namespace SamaHesab.Modules.Tourism.Application;

/// <summary>مبنای محاسبهٔ پورسانتِ فروشنده.</summary>
public enum CommissionBasis
{
    PerUnit = 0,          // مبلغِ ثابت به‌ازای هر واحد
    PercentOfSale = 1,    // درصدی از مبلغِ فروش
    PercentOfProfit = 2   // درصدی از سودِ فروش
}

/// <summary>
/// یک قاعدهٔ پورسانت. دامنهٔ اعمال با <see cref="ProductId"/>/<see cref="GroupId"/> مشخص می‌شود؛
/// قاعدهٔ پیش‌فرض هر دو را null دارد. resolve: محصول > گروه > پیش‌فرض (به‌ترتیبِ Priorityِ نزولی).
/// </summary>
public record CommissionRuleSpec(
    CommissionBasis Basis,
    decimal Rate,                 // PerUnit: مبلغِ هر واحد · درصدی‌ها: درصد (۵ = ۵٪)
    int? ProductId = null,
    int? GroupId = null,
    int Priority = 0);

/// <summary>زمینهٔ یک خطِ فروش برای محاسبهٔ پورسانت.</summary>
public record CommissionContext(
    int ProductId, int GroupId,
    decimal Quantity, decimal SaleAmount, decimal DiscountAmount, decimal CostAmount,
    bool CommissionAfterDiscount = true);

/// <summary>
/// موتورِ خالصِ پورسانت — مستقل و تست‌پذیر، بدونِ وابستگی به DB.
/// <see cref="Resolve"/> قاعدهٔ مناسبِ یک خط را می‌یابد و <see cref="Compute"/> مبلغِ پورسانت را می‌دهد.
/// </summary>
public static class CommissionEngine
{
    /// <summary>
    /// انتخابِ قاعدهٔ مؤثر برای یک خط: ابتدا قاعدهٔ مخصوصِ همان محصول، سپس گروه، سپس پیش‌فرض (هر دو null).
    /// در هر سطح، قاعده‌ای با بالاترین <see cref="CommissionRuleSpec.Priority"/> برنده است.
    /// </summary>
    public static CommissionRuleSpec? Resolve(IEnumerable<CommissionRuleSpec> rules, int productId, int groupId)
    {
        if (rules is null) return null;
        var list = rules as IReadOnlyList<CommissionRuleSpec> ?? rules.ToList();

        return Best(list.Where(r => r.ProductId == productId))
            ?? Best(list.Where(r => r.ProductId is null && r.GroupId == groupId))
            ?? Best(list.Where(r => r.ProductId is null && r.GroupId is null));
    }

    private static CommissionRuleSpec? Best(IEnumerable<CommissionRuleSpec> rules) =>
        rules.OrderByDescending(r => r.Priority).FirstOrDefault();

    /// <summary>محاسبهٔ مبلغِ پورسانتِ یک خط بر اساسِ قاعده و زمینه.</summary>
    public static decimal Compute(CommissionRuleSpec? rule, CommissionContext ctx)
    {
        if (rule is null) return 0m;

        // مبنای فروش: با لحاظِ تخفیف یا بدونِ آن (طبقِ پرچمِ تنظیمات).
        var netSale = ctx.CommissionAfterDiscount
            ? ctx.SaleAmount - ctx.DiscountAmount
            : ctx.SaleAmount;

        var commission = rule.Basis switch
        {
            CommissionBasis.PerUnit         => rule.Rate * Math.Max(0, ctx.Quantity),
            CommissionBasis.PercentOfSale   => Math.Max(0, netSale) * rule.Rate / 100m,
            CommissionBasis.PercentOfProfit => Math.Max(0, netSale - ctx.CostAmount) * rule.Rate / 100m,
            _ => 0m
        };

        return Round(Math.Max(0, commission));
    }

    /// <summary>میان‌بر: resolve + compute برای یک خط.</summary>
    public static decimal ComputeFor(IEnumerable<CommissionRuleSpec> rules, CommissionContext ctx) =>
        Compute(Resolve(rules, ctx.ProductId, ctx.GroupId), ctx);

    private static decimal Round(decimal v) => Math.Round(v, 0, MidpointRounding.AwayFromZero);
}
