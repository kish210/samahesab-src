using System.Collections.Generic;
using System.Linq;
using SamaHesab.Application.Sales.Commands;
using SamaHesab.Domain.Enums;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>فاز RC (RC-val) — کرانهٔ درصدِ تخفیف/مالیاتِ ردیف در validatorِ فاکتورِ فروش (۰..۱۰۰).</summary>
public class InvoiceDiscountBoundsTests
{
    private static CreateSalesInvoiceCommand Cmd(decimal discountPct, decimal taxPct = 9) => new(
        BranchId: 1, FiscalYearId: 1, InvoiceDate: "1405/01/01", CustomerId: 1, WarehouseId: 1,
        InvoiceType: InvoiceType.Sale, PriceLevel: "خرده", SalesRepId: null, DueDate: null,
        Description: null, Shipping: 0, OtherCosts: 0,
        Items: new List<SalesInvoiceItemDto> { new(ProductId: 1, Quantity: 1, UnitPrice: 1000, DiscountPct: discountPct, TaxPct: taxPct, Description: null, BatchId: null, SerialId: null) });

    private static bool IsValid(CreateSalesInvoiceCommand c) => new CreateSalesInvoiceCommandValidator().Validate(c).IsValid;

    [Fact] public void Normal_Discount_Passes() => Assert.True(IsValid(Cmd(20)));
    [Fact] public void Over_100_Discount_Fails() => Assert.False(IsValid(Cmd(150)));
    [Fact] public void Negative_Discount_Fails() => Assert.False(IsValid(Cmd(-5)));
    [Fact] public void Over_100_Tax_Fails() => Assert.False(IsValid(Cmd(0, taxPct: 120)));
}
