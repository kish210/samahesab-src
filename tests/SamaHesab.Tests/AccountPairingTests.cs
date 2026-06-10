using SamaHesab.Application.Accounting;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>پیشنهاد هوشمند حساب طرفِ مقابل (Smart Account Suggestions) — منطق خالص.</summary>
public class AccountPairingTests
{
    private static IReadOnlyCollection<int> V(params int[] ids) => ids;

    [Fact]
    public void Suggests_Most_Frequent_Counter_Account_First()
    {
        var history = new[]
        {
            V(10, 20),   // 10 با 20
            V(10, 20),   // 10 با 20
            V(10, 30),   // 10 با 30
            V(40, 50),   // بی‌ربط
        };

        var result = AccountPairing.Suggest(history, forAccountId: 10, top: 6);

        Assert.Equal(20, result[0].AccountId);   // پرتکرارترین جفت
        Assert.Equal(2, result[0].Count);
        Assert.Equal(30, result[1].AccountId);
        Assert.Equal(1, result[1].Count);
        Assert.DoesNotContain(result, s => s.AccountId == 40 || s.AccountId == 50);
    }

    [Fact]
    public void Excludes_The_Account_Itself()
    {
        var history = new[] { V(10, 10, 20) };   // 10 تکراری در یک سند
        var result = AccountPairing.Suggest(history, 10, 6);
        Assert.DoesNotContain(result, s => s.AccountId == 10);
        Assert.Single(result);
        Assert.Equal(20, result[0].AccountId);
    }

    [Fact]
    public void Honors_Top_Limit()
    {
        var history = new[] { V(1, 2, 3, 4, 5, 6, 7, 8) };
        var result = AccountPairing.Suggest(history, 1, top: 3);
        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void Empty_When_Account_Never_Used()
    {
        var history = new[] { V(10, 20) };
        Assert.Empty(AccountPairing.Suggest(history, forAccountId: 99, top: 6));
    }
}
