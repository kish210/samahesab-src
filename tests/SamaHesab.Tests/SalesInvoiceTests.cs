using SamaHesab.Domain.Entities.Sales;
using SamaHesab.Domain.Enums;
using SamaHesab.Domain.Events;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>قرارداد وضعیت فاکتور فروش — مبنای «Post خودکار» (#۲۵).</summary>
public class SalesInvoiceTests
{
    private static SalesInvoice NewInvoice() =>
        SalesInvoice.Create(companyId: 1, branchId: 1, fiscalYearId: 1,
            invoiceNumber: "F0001", invoiceDate: "1405/03/21", customerId: 5, warehouseId: 1);

    [Fact]
    public void New_Invoice_Starts_Draft()
        => Assert.Equal(InvoiceStatus.Draft, NewInvoice().Status);

    [Fact]
    public void Post_Sets_Posted_Links_Voucher_And_Raises_Event()
    {
        var inv = NewInvoice();
        inv.Post(userId: 7, voucherId: 42);

        Assert.Equal(InvoiceStatus.Posted, inv.Status);
        Assert.Equal(42, inv.VoucherId);
        Assert.Contains(inv.DomainEvents, e => e is SalesInvoicePostedEvent);
    }

    [Fact]
    public void Posted_Invoice_Cannot_Be_Posted_Again()
    {
        var inv = NewInvoice();
        inv.Post(1, 1);
        Assert.Throws<System.InvalidOperationException>(() => inv.Post(1, 2));
    }
}
