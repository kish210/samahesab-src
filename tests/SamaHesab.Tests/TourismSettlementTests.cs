using SamaHesab.Modules.Tourism.Application;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>موتور تسویه‌ی حسابداریِ گردشگری (M11).</summary>
public class TourismSettlementTests
{
    [Fact]
    public void FullyPaid_Sale_With_Cost_And_Commission_Balances()
    {
        // فروش ۱۰٬۰۰۰٬۰۰۰، بهای تأمین ۷٬۰۰۰٬۰۰۰، کمیسیون ۵٪، تمام نقدی
        var r = TourismSettlement.Build(10_000_000, 7_000_000, 5, 10_000_000);
        Assert.True(r.IsBalanced);
        Assert.Equal(r.TotalDebit, r.TotalCredit);
        Assert.Equal(500_000, r.Commission);              // ۵٪ از ۱۰م
        Assert.Equal(2_500_000, r.NetProfit);             // ۱۰م − ۷م − ۰٫۵م
        // نقدی کامل: ردیف دریافتنی نباید باشد
        Assert.DoesNotContain(r.Lines, l => l.Role == TourismAccountRole.Receivable);
        Assert.Contains(r.Lines, l => l.Role == TourismAccountRole.Cash && l.Debit == 10_000_000);
    }

    [Fact]
    public void PartiallyPaid_Splits_Cash_And_Receivable()
    {
        var r = TourismSettlement.Build(10_000_000, 0, 0, 4_000_000);  // ۴م نقد، ۶م نسیه
        Assert.True(r.IsBalanced);
        Assert.Contains(r.Lines, l => l.Role == TourismAccountRole.Cash && l.Debit == 4_000_000);
        Assert.Contains(r.Lines, l => l.Role == TourismAccountRole.Receivable && l.Debit == 6_000_000);
        Assert.Contains(r.Lines, l => l.Role == TourismAccountRole.TourismRevenue && l.Credit == 10_000_000);
    }

    [Fact]
    public void Credit_Sale_No_Cash_Line()
    {
        var r = TourismSettlement.Build(5_000_000, 3_000_000, 0, 0);   // کاملاً نسیه
        Assert.True(r.IsBalanced);
        Assert.DoesNotContain(r.Lines, l => l.Role == TourismAccountRole.Cash);
        Assert.Contains(r.Lines, l => l.Role == TourismAccountRole.Receivable && l.Debit == 5_000_000);
        Assert.Contains(r.Lines, l => l.Role == TourismAccountRole.SupplierPayable && l.Credit == 3_000_000);
    }

    [Fact]
    public void Overpayment_Capped_To_SaleAmount()
    {
        var r = TourismSettlement.Build(5_000_000, 0, 0, 9_000_000);   // پرداخت بیش از فروش
        Assert.True(r.IsBalanced);
        Assert.Contains(r.Lines, l => l.Role == TourismAccountRole.Cash && l.Debit == 5_000_000);
        Assert.DoesNotContain(r.Lines, l => l.Role == TourismAccountRole.Receivable);
    }

    [Fact]
    public void Commission_Block_Balances_By_Itself()
    {
        var r = TourismSettlement.Build(1_000_000, 0, 10, 1_000_000);
        Assert.Equal(100_000, r.Commission);
        Assert.Contains(r.Lines, l => l.Role == TourismAccountRole.CommissionExpense && l.Debit == 100_000);
        Assert.Contains(r.Lines, l => l.Role == TourismAccountRole.CommissionPayable && l.Credit == 100_000);
        Assert.True(r.IsBalanced);
    }
}
