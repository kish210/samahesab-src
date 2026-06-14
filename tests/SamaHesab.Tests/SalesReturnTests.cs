using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Domain.Entities.Sales;
using SamaHesab.Domain.Enums;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>
/// T13 — مرجوعیِ فروش (POS): راستی‌آزماییِ موجودیتِ فاکتورِ برگشت + ترازِ سندِ معکوسِ فروش.
/// منطقِ سندِ ساخته‌شده در <c>TryCreateSalesReturnVoucherAsync</c> اینجا به‌صورت خالصِ دامنه بازسازی
/// می‌شود تا تراز بودن (بدهکار=بستانکار) و جهتِ درستِ حساب‌ها تضمین شود (بدونِ EF/ریپازیتوری).
/// در برگشت، جهتِ سندِ فروش معکوس می‌شود: بدهکار «درآمد فروش» + «مالیات»، بستانکار «صندوق»/«دریافتنی».
/// </summary>
public class SalesReturnTests
{
    private static SalesInvoice NewReturn(decimal qty, decimal price, decimal taxPct)
    {
        var inv = SalesInvoice.Create(companyId: 1, branchId: 1, fiscalYearId: 1,
            invoiceNumber: "BR0001", invoiceDate: "1405/03/21", customerId: 5, warehouseId: 1,
            invoiceType: InvoiceType.SaleReturn);
        inv.AddItem(SalesInvoiceItem.Create(0, 1, productId: 9, quantity: qty, unitPrice: price, discountPct: 0, taxPct: taxPct));
        return inv;
    }

    [Fact]
    public void Return_Invoice_Has_SaleReturn_Type_And_Totals()
    {
        var inv = NewReturn(qty: 2, price: 1500, taxPct: 9);  // 3000 + 9% = 3270
        Assert.Equal(InvoiceType.SaleReturn, inv.InvoiceType);
        Assert.Equal(3000, inv.SubTotal);
        Assert.Equal(270, inv.TotalTax);
        Assert.Equal(3270, inv.GrandTotal);
    }

    [Fact]
    public void Reverse_Voucher_Is_Balanced_On_Cash_Refund()
    {
        // بازپرداختِ نقدی: بدهکار درآمد فروش + مالیات / بستانکار صندوق — باید متوازن و قابلِ ثبت باشد.
        var inv = NewReturn(qty: 2, price: 2500, taxPct: 9); // 5000 + 9% = 5450
        var salesAmount = inv.GrandTotal - inv.TotalTax;      // 5000

        var v = Voucher.Create(1, 1, 1, "V200", "1405/03/21", 3, "سند خودکار برگشت از فروش BR0001", "BR0001");
        v.AddItem(VoucherItem.Create(0, 1, accountId: 61 /*درآمد فروش*/, debit: salesAmount, credit: 0, "برگشت از فروش"));
        v.AddItem(VoucherItem.Create(0, 2, accountId: 34 /*مالیات*/, debit: inv.TotalTax, credit: 0, "برگشتِ مالیات"));
        v.AddItem(VoucherItem.Create(0, 3, accountId: 11 /*صندوق*/, debit: 0, credit: inv.GrandTotal, "بازپرداخت نقدی"));

        Assert.Equal(5450, v.TotalDebit);
        Assert.Equal(v.TotalDebit, v.TotalCredit);
        Assert.True(v.CanPost());   // پیش‌نویس + متوازن + دارای ردیف
    }

    [Fact]
    public void Reverse_Voucher_Is_Balanced_On_Credit_Return()
    {
        // برگشتِ نسیه (بدونِ بازپرداختِ نقد): بدهکار درآمد فروش / بستانکار حساب دریافتنیِ مشتری.
        var inv = NewReturn(qty: 1, price: 12000, taxPct: 0);

        var v = Voucher.Create(1, 1, 1, "V201", "1405/03/21", 3, "سند خودکار برگشت از فروش BR0002", "BR0002");
        v.AddItem(VoucherItem.Create(0, 1, accountId: 61, debit: inv.GrandTotal, credit: 0, "برگشت از فروش"));
        v.AddItem(VoucherItem.Create(0, 2, accountId: 13 /*دریافتنی*/, debit: 0, credit: inv.GrandTotal, "کاهش مطالبه از مشتری"));

        Assert.Equal(12000, v.TotalDebit);
        Assert.Equal(v.TotalDebit, v.TotalCredit);
        Assert.True(v.CanPost());
    }
}
