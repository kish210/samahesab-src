using SamaHesab.Application.Treasury;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>تخصیص خودکار FIFO وجهِ دریافتی به فاکتورهای باز — گردش‌کار وصول مطالبات.</summary>
public class PaymentAllocationTests
{
    private static (int, decimal)[] Invoices(params (int id, decimal rem)[] xs) => xs;

    [Fact]
    public void Allocates_Oldest_First_And_Spills_To_Next()
    {
        var (lines, unapplied) = PaymentAllocation.AllocateFifo(
            1500, Invoices((1, 1000), (2, 1000)));

        Assert.Equal(0, unapplied);
        Assert.Equal(2, lines.Count);
        Assert.Equal(1000, lines[0].Applied);   // فاکتور قدیمی کامل
        Assert.Equal(1, lines[0].InvoiceId);
        Assert.Equal(500, lines[1].Applied);     // مابقی روی فاکتور بعدی
        Assert.Equal(2, lines[1].InvoiceId);
    }

    [Fact]
    public void Partial_Payment_Touches_Only_First_Invoice()
    {
        var (lines, unapplied) = PaymentAllocation.AllocateFifo(
            400, Invoices((1, 1000), (2, 1000)));

        Assert.Equal(0, unapplied);
        Assert.Single(lines);
        Assert.Equal(400, lines[0].Applied);
        Assert.Equal(1, lines[0].InvoiceId);
    }

    [Fact]
    public void Overpayment_Leaves_Unapplied_Remainder()
    {
        var (lines, unapplied) = PaymentAllocation.AllocateFifo(
            2500, Invoices((1, 1000), (2, 1000)));

        Assert.Equal(2, lines.Count);
        Assert.Equal(500, unapplied);            // ۵۰۰ علی‌الحساب باقی می‌ماند
    }

    [Fact]
    public void Exact_Payment_Fully_Settles_Single_Invoice()
    {
        var (lines, unapplied) = PaymentAllocation.AllocateFifo(1000, Invoices((7, 1000)));
        Assert.Single(lines);
        Assert.Equal(1000, lines[0].Applied);
        Assert.Equal(0, unapplied);
    }

    [Fact]
    public void No_Open_Invoices_Leaves_Everything_Unapplied()
    {
        var (lines, unapplied) = PaymentAllocation.AllocateFifo(1000, Invoices());
        Assert.Empty(lines);
        Assert.Equal(1000, unapplied);
    }

    [Fact]
    public void Skips_Invoices_Without_Remaining()
    {
        var (lines, unapplied) = PaymentAllocation.AllocateFifo(
            500, Invoices((1, 0), (2, 800)));

        Assert.Single(lines);
        Assert.Equal(2, lines[0].InvoiceId);
        Assert.Equal(500, lines[0].Applied);
        Assert.Equal(0, unapplied);
    }
}
