using SamaHesab.Application.Accounting;
using SamaHesab.Domain.Entities.Sales;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>منطق دامنه‌ی فاکتور تکرارشونده + بازاستفاده از زمان‌بندی.</summary>
public class RecurringInvoiceTests
{
    [Fact]
    public void Create_With_Lines_And_MarkGenerated_Advances_NextDate()
    {
        var ri = RecurringInvoice.Create(1, 1, "اجاره ماهانه", customerId: 5, warehouseId: 1,
            frequency: 0 /*ماهانه*/, nextDate: "1404/01/01");
        ri.AddLine(productId: 10, quantity: 1, unitPrice: 5_000_000);
        Assert.Single(ri.Lines);

        var next = RecurrenceSchedule.NextAfter(ri.NextDate, RecurrenceFrequency.Monthly);
        ri.MarkGenerated("1404/01/01", next);

        Assert.Equal("1404/01/01", ri.LastGeneratedDate);
        Assert.Equal("1404/02/01", ri.NextDate);
    }

    [Fact]
    public void AddLine_Rejects_NonPositive_Quantity()
    {
        var ri = RecurringInvoice.Create(1, 1, "x", 1, 1, 0, "1404/01/01");
        Assert.Throws<ArgumentException>(() => ri.AddLine(1, 0, 100));
    }

    [Fact]
    public void Yearly_Frequency_Advances_Year()
    {
        var ri = RecurringInvoice.Create(1, 1, "بیمه سالانه", 5, 1, frequency: 1, nextDate: "1404/05/10");
        var next = RecurrenceSchedule.NextAfter(ri.NextDate, RecurrenceFrequency.Yearly);
        Assert.Equal("1405/05/10", next);
    }

    [Fact]
    public void Deactivate_Sets_Inactive()
    {
        var ri = RecurringInvoice.Create(1, 1, "x", 1, 1, 0, "1404/01/01");
        ri.Deactivate();
        Assert.False(ri.IsActive);
    }
}
