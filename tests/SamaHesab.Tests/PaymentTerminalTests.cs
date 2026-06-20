using System.Text.RegularExpressions;
using SamaHesab.Application.Payments;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>💳 CR-1 — درایورِ شبیه‌سازِ ترمینالِ کارت‌خوان.</summary>
public class PaymentTerminalTests
{
    [Fact]
    public async Task Pay_Approves_And_Returns_12Digit_Rrn()
    {
        IPaymentTerminalService t = new SimulatedPaymentTerminal("T-77");
        var res = await t.PayAsync(new CardPaymentRequest(150_000, "F-1001"));

        Assert.True(res.Approved);
        Assert.Equal(150_000, res.Amount);
        Assert.Matches("^[0-9]{12}$", res.Rrn);          // RRN ۱۲ رقمی
        Assert.Equal("T-77", res.TerminalId);
        Assert.False(string.IsNullOrWhiteSpace(res.MaskedPan));
        Assert.True(t.IsReady);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-500)]
    public async Task Pay_NonPositive_Is_Declined(decimal amount)
    {
        IPaymentTerminalService t = new SimulatedPaymentTerminal();
        var res = await t.PayAsync(new CardPaymentRequest(amount));
        Assert.False(res.Approved);
        Assert.Null(res.Rrn);
    }

    [Fact]
    public async Task Refund_Returns_Approved_With_Same_Rrn()
    {
        IPaymentTerminalService t = new SimulatedPaymentTerminal();
        var res = await t.RefundAsync("123456789012", 50_000);
        Assert.True(res.Approved);
        Assert.Equal("123456789012", res.Rrn);
        Assert.Equal(50_000, res.Amount);
    }
}
