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

    /// <summary>
    /// U-INV-DISC — قبلاً InvoiceDiscount (تخفیفِ کلِ فاکتور) هرگز رویِ خودِ فاکتور اعمال نمی‌شد؛
    /// فقط سندِ حسابداری آن را می‌دید. یک فاکتورِ ۱۰۰۰ با تخفیفِ ۱۰۰ که ۹۰۰ پرداخت شده بود
    /// (دقیقاً مبلغِ خالص) یک ماندهٔ شبح‌وارِ ۱۰۰ نشان می‌داد — این تست همان سناریو را قفل می‌کند.
    /// </summary>
    [Fact]
    public void Invoice_Discount_Is_Reflected_In_GrandTotal_And_Remain()
    {
        var inv = NewInvoice();
        inv.AddItem(SalesInvoiceItem.Create(0, 1, productId: 10, quantity: 1, unitPrice: 1000));
        inv.SetInvoiceDiscount(100);

        Assert.Equal(900, inv.GrandTotal);

        inv.AddPayment(900);   // پرداختِ کاملِ مبلغِ خالص (پس از تخفیف)

        Assert.Equal(0, inv.RemainAmount);
        Assert.True(inv.IsFullyPaid());
    }

    [Fact]
    public void Invoice_Discount_Larger_Than_Total_Clamps_GrandTotal_To_Zero()
    {
        var inv = NewInvoice();
        inv.AddItem(SalesInvoiceItem.Create(0, 1, productId: 10, quantity: 1, unitPrice: 500));
        inv.SetInvoiceDiscount(1000);   // تخفیفِ بزرگ‌تر از مبلغِ فاکتور

        Assert.Equal(0, inv.GrandTotal);
    }
}
