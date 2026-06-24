using SamaHesab.Modules.Tourism.Application;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>TUR-C2-1 — موتورِ خالصِ پورسانت.</summary>
public class CommissionEngineTests
{
    [Fact]
    public void PerUnit_Multiplies_Rate_By_Quantity()
    {
        var rule = new CommissionRuleSpec(CommissionBasis.PerUnit, Rate: 50_000);
        var ctx = new CommissionContext(1, 1, Quantity: 3, SaleAmount: 0, DiscountAmount: 0, CostAmount: 0);
        Assert.Equal(150_000m, CommissionEngine.Compute(rule, ctx));
    }

    [Fact]
    public void PercentOfSale_After_Discount()
    {
        var rule = new CommissionRuleSpec(CommissionBasis.PercentOfSale, Rate: 5);
        var ctx = new CommissionContext(1, 1, 1, SaleAmount: 1_000_000, DiscountAmount: 200_000,
            CostAmount: 0, CommissionAfterDiscount: true);
        Assert.Equal(40_000m, CommissionEngine.Compute(rule, ctx));  // ۵٪ از ۸۰۰٬۰۰۰
    }

    [Fact]
    public void PercentOfSale_Before_Discount()
    {
        var rule = new CommissionRuleSpec(CommissionBasis.PercentOfSale, Rate: 5);
        var ctx = new CommissionContext(1, 1, 1, SaleAmount: 1_000_000, DiscountAmount: 200_000,
            CostAmount: 0, CommissionAfterDiscount: false);
        Assert.Equal(50_000m, CommissionEngine.Compute(rule, ctx));  // ۵٪ از ۱٬۰۰۰٬۰۰۰
    }

    [Fact]
    public void PercentOfProfit_Uses_Net_Minus_Cost()
    {
        var rule = new CommissionRuleSpec(CommissionBasis.PercentOfProfit, Rate: 10);
        var ctx = new CommissionContext(1, 1, 1, SaleAmount: 1_000_000, DiscountAmount: 0,
            CostAmount: 700_000, CommissionAfterDiscount: true);
        Assert.Equal(30_000m, CommissionEngine.Compute(rule, ctx));  // ۱۰٪ از سودِ ۳۰۰٬۰۰۰
    }

    [Fact]
    public void Negative_Profit_Yields_Zero_Commission()
    {
        var rule = new CommissionRuleSpec(CommissionBasis.PercentOfProfit, Rate: 10);
        var ctx = new CommissionContext(1, 1, 1, SaleAmount: 500_000, DiscountAmount: 0, CostAmount: 700_000);
        Assert.Equal(0m, CommissionEngine.Compute(rule, ctx));
    }

    [Fact]
    public void Resolve_Prefers_Product_Then_Group_Then_Default()
    {
        var rules = new[]
        {
            new CommissionRuleSpec(CommissionBasis.PercentOfSale, 1, ProductId: null, GroupId: null), // پیش‌فرض
            new CommissionRuleSpec(CommissionBasis.PercentOfSale, 2, ProductId: null, GroupId: 7),     // گروه
            new CommissionRuleSpec(CommissionBasis.PercentOfSale, 3, ProductId: 42),                   // محصول
        };
        Assert.Equal(3m, CommissionEngine.Resolve(rules, productId: 42, groupId: 7)!.Rate);  // محصول
        Assert.Equal(2m, CommissionEngine.Resolve(rules, productId: 99, groupId: 7)!.Rate);  // گروه
        Assert.Equal(1m, CommissionEngine.Resolve(rules, productId: 99, groupId: 8)!.Rate);  // پیش‌فرض
    }

    [Fact]
    public void Resolve_Honors_Priority_Within_Same_Scope()
    {
        var rules = new[]
        {
            new CommissionRuleSpec(CommissionBasis.PerUnit, 10, ProductId: 5, Priority: 1),
            new CommissionRuleSpec(CommissionBasis.PerUnit, 20, ProductId: 5, Priority: 5),
        };
        Assert.Equal(20m, CommissionEngine.Resolve(rules, 5, 0)!.Rate);
    }

    [Fact]
    public void No_Matching_Rule_Returns_Null_And_Zero()
    {
        var rules = new[] { new CommissionRuleSpec(CommissionBasis.PerUnit, 10, ProductId: 5) };
        Assert.Null(CommissionEngine.Resolve(rules, productId: 9, groupId: 9));
        Assert.Equal(0m, CommissionEngine.ComputeFor(rules,
            new CommissionContext(9, 9, 1, 1_000_000, 0, 0)));
    }
}
