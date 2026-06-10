using SamaHesab.Domain.Entities.POS;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>کار #۳۳ — فاکتور معلق (Hold/Recall) — منطق دامنه.</summary>
public class HeldSaleTests
{
    [Fact]
    public void Create_Holds_Cart()
    {
        var h = HeldSale.Create(1, 1, 7, "مشتری در صف", "[{\"p\":1,\"q\":2}]", 500000);
        Assert.Equal("مشتری در صف", h.Label);
        Assert.Equal(500000, h.Total);
        Assert.False(string.IsNullOrEmpty(h.Payload));
    }

    [Theory]
    [InlineData("", "[]")]
    [InlineData("x", "")]
    public void Create_Validates(string label, string payload)
        => Assert.Throws<ArgumentException>(() => HeldSale.Create(1, 1, 7, label, payload, 0));
}
