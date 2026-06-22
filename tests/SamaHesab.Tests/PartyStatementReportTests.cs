using System.Linq;
using SamaHesab.Application.Reports;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>CR-C1 — گزارشِ خروجی‌دارِ اظهارِ حسابِ مشتری/تأمین‌کننده.</summary>
public class PartyStatementReportTests
{
    [Fact]
    public void Statement_Has_Header_Rows_Totals_And_Closing()
    {
        var t = PartyStatementReportBuilder.Build("علی احمدی", new[]
        {
            new PartyStatementLine("1404/01/05", "فاکتور فروش", "1001", "خرید", 5_000_000, 0, 5_000_000),
            new PartyStatementLine("1404/01/20", "دریافت", "1001", "نقد", 0, 2_000_000, 3_000_000),
        }, totalDebit: 5_000_000, totalCredit: 2_000_000, closingBalance: 3_000_000);

        Assert.Contains("اظهارِ حسابِ مشتری", t.Title);
        Assert.Equal(7, t.Headers.Count);                       // تاریخ..مانده
        Assert.Equal(4, t.Rows.Count);                          // ۲ تراکنش + جمع + مانده
        Assert.Contains(t.Rows, r => r[3] == "جمعِ گردش" && r[4] == "5,000,000" && r[5] == "2,000,000");
        Assert.Contains(t.Rows, r => r[3] == "ماندهٔ بدهکارِ مشتری" && r[6] == "3,000,000");
    }

    [Fact]
    public void Supplier_Statement_Uses_Supplier_Labels()
    {
        var t = PartyStatementReportBuilder.Build("شرکتِ پخش", System.Array.Empty<PartyStatementLine>(),
            0, 0, -1_500_000, isSupplier: true);
        Assert.Contains("اظهارِ حسابِ تأمین‌کننده", t.Title);
        Assert.Contains(t.Rows, r => r[3] == "ماندهٔ طلبِ ما از تأمین‌کننده" && r[6] == "1,500,000");
    }
}
