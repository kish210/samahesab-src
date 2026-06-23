using System.Linq;
using SamaHesab.Application.Accounting;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>دفترِ معینِ حساب — ماندهٔ روان + اشارهٔ هر ردیف به سندِ مرجع (drill-down).</summary>
public class AccountLedgerTests
{
    private static AccountLedgerRawLine L(int vid, string num, string date, decimal d, decimal c)
        => new(vid, num, date, d, c, "شرح");

    [Fact]
    public void Running_Balance_Accumulates_From_Opening()
    {
        var r = AccountLedger.Build(1000, new[]
        {
            L(1, "101", "1405/01/05", 500, 0),    // 1500
            L(2, "102", "1405/01/10", 0, 200),    // 1300
        });

        Assert.Equal(1000, r.OpeningBalance);
        Assert.Equal(1500, r.Rows[0].Balance);
        Assert.Equal(1300, r.Rows[1].Balance);
        Assert.Equal(1300, r.ClosingBalance);
        Assert.Equal(500, r.TotalDebit);
        Assert.Equal(200, r.TotalCredit);
    }

    [Fact]
    public void Rows_Sorted_By_Date_Then_VoucherNumber()
    {
        var r = AccountLedger.Build(0, new[]
        {
            L(3, "205", "1405/02/01", 100, 0),
            L(1, "203", "1405/01/15", 50, 0),
            L(2, "204", "1405/01/15", 70, 0),
        });

        Assert.Equal(new[] { "203", "204", "205" }, r.Rows.Select(x => x.VoucherNumber).ToArray());
    }

    [Fact]
    public void Each_Row_Carries_Voucher_Reference_For_Drilldown()
    {
        var r = AccountLedger.Build(0, new[] { L(42, "999", "1405/03/01", 10, 0) });
        Assert.Equal(42, r.Rows[0].VoucherId);
        Assert.Equal("999", r.Rows[0].VoucherNumber);
    }

    [Fact]
    public void Empty_Ledger_Returns_Opening_As_Closing()
    {
        var r = AccountLedger.Build(750, System.Array.Empty<AccountLedgerRawLine>());
        Assert.Empty(r.Rows);
        Assert.Equal(750, r.ClosingBalance);
        Assert.Equal(0, r.TotalDebit);
    }
}
