using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Domain.Entities.Purchase;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>
/// C2-C — مرجوعی خرید: راستی‌آزماییِ موجودیتِ فاکتورِ مرجوعی + ترازِ سندِ معکوسِ خرید.
/// منطقِ سندِ ساخته‌شده در <c>CreatePurchaseReturnCommandHandler</c> اینجا به‌صورت خالصِ دامنه بازسازی
/// می‌شود تا تراز بودن (بدهکار=بستانکار) و درستیِ مبالغ تضمین شود (بدونِ نیاز به EF/ریپازیتوری).
/// </summary>
public class PurchaseReturnTests
{
    private static PurchaseInvoice NewReturn(decimal qty, decimal price, decimal taxPct)
    {
        var inv = PurchaseInvoice.Create(1, 1, 1, "PR000001", "1404/06/01",
            supplierId: 5, warehouseId: 2, invoiceType: "برگشت خرید");
        inv.AddItem(PurchaseInvoiceItem.Create(0, 1, productId: 9, quantity: qty, unitPrice: price, discountPct: 0, taxPct: taxPct));
        inv.SetShipping(0, 0);
        return inv;
    }

    [Fact]
    public void Return_Invoice_Computes_GrandTotal_With_Tax()
    {
        var inv = NewReturn(qty: 3, price: 1000, taxPct: 10);  // 3000 + 10% = 3300
        Assert.Equal(3000, inv.SubTotal);
        Assert.Equal(300, inv.TotalTax);
        Assert.Equal(3300, inv.GrandTotal);
    }

    [Fact]
    public void SetReturnedFrom_Links_Original_Invoice()
    {
        var inv = NewReturn(1, 500, 0);
        inv.SetReturnedFrom(42);
        Assert.Equal(42, inv.ReturnedFromId);
    }

    [Fact]
    public void Reverse_Voucher_Is_Balanced_On_Credit_Return()
    {
        // مرجوعیِ نسیه: بدهکار پرداختنی / بستانکار موجودی — باید متوازن و قابلِ ثبت باشد.
        var inv = NewReturn(qty: 2, price: 2500, taxPct: 9); // 5000 + 9% = 5450
        var v = Voucher.Create(1, 1, 1, "V100", "1404/06/01", 4, "سند خودکار مرجوعی خرید PR000001", "PR000001");
        v.AddItem(VoucherItem.Create(0, 1, accountId: 31 /*پرداختنی*/, debit: inv.GrandTotal, credit: 0, "کاهش بدهی به تأمین‌کننده"));
        v.AddItem(VoucherItem.Create(0, 2, accountId: 15 /*موجودی کالا*/, debit: 0, credit: inv.GrandTotal, "برگشت کالا به تأمین‌کننده"));

        Assert.Equal(5450, v.TotalDebit);
        Assert.Equal(5450, v.TotalCredit);
        Assert.True(v.CanPost());   // پیش‌نویس + متوازن + دارای ردیف
    }

    [Fact]
    public void Reverse_Voucher_Is_Balanced_On_Cash_Refund()
    {
        // مرجوعیِ نقدی: بدهکار صندوق / بستانکار موجودی.
        var inv = NewReturn(qty: 1, price: 12000, taxPct: 0);
        var v = Voucher.Create(1, 1, 1, "V101", "1404/06/01", 4, "سند خودکار مرجوعی خرید PR000002", "PR000002");
        v.AddItem(VoucherItem.Create(0, 1, accountId: 11 /*صندوق*/, debit: inv.GrandTotal, credit: 0, "دریافت نقدی از تأمین‌کننده"));
        v.AddItem(VoucherItem.Create(0, 2, accountId: 15, debit: 0, credit: inv.GrandTotal, "برگشت کالا به تأمین‌کننده"));

        Assert.Equal(12000, v.TotalDebit);
        Assert.Equal(v.TotalDebit, v.TotalCredit);
        Assert.True(v.CanPost());
    }
}
