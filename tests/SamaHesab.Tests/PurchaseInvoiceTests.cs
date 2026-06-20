using SamaHesab.Domain.Entities.Purchase;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>کار #۵ — قرارداد وضعیت فاکتور خرید: «Confirm» بدونِ سند، فاکتور را از «پیش‌نویس» خارج می‌کند.</summary>
public class PurchaseInvoiceTests
{
    private static PurchaseInvoice New() =>
        PurchaseInvoice.Create(companyId: 1, branchId: 1, fiscalYearId: 1,
            invoiceNumber: "K0001", invoiceDate: "1405/03/21", supplierId: 9, warehouseId: 1);

    [Fact]
    public void New_Invoice_Starts_Draft()
        => Assert.Equal("پیش‌نویس", New().StatusCode);

    [Fact]
    public void Confirm_From_Draft_Sets_Ghatti_Without_Voucher()
    {
        var inv = New();
        inv.Confirm();
        Assert.Equal("قطعی", inv.StatusCode);   // از «پیش‌نویس» خارج شد
        Assert.Null(inv.VoucherId);             // بدونِ سندِ حسابداری
    }

    [Fact]
    public void Confirm_Does_Not_Downgrade_Posted()
    {
        var inv = New();
        inv.Post(voucherId: 42);
        inv.Confirm();                          // نباید وضعیتِ قطعیِ سند‌دار را تغییر دهد
        Assert.Equal("قطعی", inv.StatusCode);
        Assert.Equal(42, inv.VoucherId);
    }
}
