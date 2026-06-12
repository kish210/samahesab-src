using SamaHesab.Application.Reports;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>موتور و طبقه‌بندیِ صورت جریان وجوه نقد (TR-1).</summary>
public class CashFlowEngineTests
{
    [Fact]
    public void IsCash_Only_For_Group_1_01()
    {
        Assert.True(CashFlowClassifier.IsCash("1-01-001"));
        Assert.True(CashFlowClassifier.IsCash("1-01-003"));
        Assert.False(CashFlowClassifier.IsCash("1-03-001"));
        Assert.False(CashFlowClassifier.IsCash("6-01-001"));
    }

    [Fact]
    public void Categorize_Operating_For_Revenue_Or_Receivable()
    {
        Assert.Equal(CashFlowCategory.Operating, CashFlowClassifier.Categorize(new[] { "6-01-001" }));
        Assert.Equal(CashFlowCategory.Operating, CashFlowClassifier.Categorize(new[] { "1-03-001" }));
        Assert.Equal(CashFlowCategory.Operating, CashFlowClassifier.Categorize(new[] { "8-02" }));
    }

    [Fact]
    public void Categorize_Investing_For_Fixed_Assets_Group2()
    {
        Assert.Equal(CashFlowCategory.Investing, CashFlowClassifier.Categorize(new[] { "2-02" }));   // ساختمان
        Assert.Equal(CashFlowCategory.Investing, CashFlowClassifier.Categorize(new[] { "2-04" }));   // وسایل نقلیه
    }

    [Fact]
    public void Categorize_Financing_For_Loans_And_Capital()
    {
        Assert.Equal(CashFlowCategory.Financing, CashFlowClassifier.Categorize(new[] { "4-01" }));      // تسهیلات بلندمدت
        Assert.Equal(CashFlowCategory.Financing, CashFlowClassifier.Categorize(new[] { "5-01" }));      // سرمایه
        Assert.Equal(CashFlowCategory.Financing, CashFlowClassifier.Categorize(new[] { "3-07" }));      // تسهیلات کوتاه‌مدت
    }

    [Fact]
    public void Categorize_Precedence_Financing_Over_Investing_Over_Operating()
    {
        // اگر هم تأمین‌مالی و هم سرمایه‌گذاری در طرفِ مقابل باشد، تأمین‌مالی غالب است.
        Assert.Equal(CashFlowCategory.Financing,
            CashFlowClassifier.Categorize(new[] { "6-01-001", "2-02", "4-01" }));
        Assert.Equal(CashFlowCategory.Investing,
            CashFlowClassifier.Categorize(new[] { "6-01-001", "2-02" }));
    }

    [Fact]
    public void Build_Sums_By_Category_And_Computes_NetChange()
    {
        var movements = new[]
        {
            new CashMovement(10_000_000, new[] { "6-01-001" }),   // عملیاتی: دریافت فروش نقدی
            new CashMovement(-3_000_000, new[] { "8-02" }),       // عملیاتی: پرداخت اجاره
            new CashMovement(-50_000_000, new[] { "2-02" }),      // سرمایه‌گذاری: خرید ساختمان
            new CashMovement(80_000_000, new[] { "4-01" }),       // تأمین‌مالی: دریافت وام
        };

        var r = CashFlowEngine.Build(movements);

        Assert.Equal(7_000_000, r.Operating);     // ۱۰م − ۳م
        Assert.Equal(-50_000_000, r.Investing);
        Assert.Equal(80_000_000, r.Financing);
        Assert.Equal(37_000_000, r.NetChange);    // ۷م − ۵۰م + ۸۰م
    }

    [Fact]
    public void Empty_Movements_Yield_Zero()
    {
        var r = CashFlowEngine.Build(System.Array.Empty<CashMovement>());
        Assert.Equal(0, r.NetChange);
    }
}
