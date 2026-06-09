using SamaHesab.Application.Accounting;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>موتور تطبیق خودکار صورت‌حساب بانک + پارسر — اولویت #۶ حسابداری.</summary>
public class BankReconciliationTests
{
    [Fact]
    public void Matches_By_Amount_And_Date()
    {
        var ledger = new[]
        {
            new LedgerLine(1, "1404/03/01", 1_000_000),
            new LedgerLine(2, "1404/03/02", 500_000),
        };
        var statement = new[]
        {
            new StatementLine("1404/03/01", 1_000_000),
            new StatementLine("1404/03/02", 500_000),
        };

        var r = BankReconciliation.AutoMatch(ledger, statement);

        Assert.Equal(2, r.Matched.Count);
        Assert.Empty(r.UnmatchedLedger);
        Assert.Empty(r.UnmatchedStatement);
    }

    [Fact]
    public void Leaves_NonMatching_On_Both_Sides()
    {
        var ledger = new[]
        {
            new LedgerLine(1, "1404/03/01", 1_000_000),
            new LedgerLine(2, "1404/03/05", 250_000),   // در صورت‌حساب نیست
        };
        var statement = new[]
        {
            new StatementLine("1404/03/01", 1_000_000),
            new StatementLine("1404/03/09", 999_000),   // در دفتر نیست
        };

        var r = BankReconciliation.AutoMatch(ledger, statement);

        Assert.Single(r.Matched);
        Assert.Single(r.UnmatchedLedger);
        Assert.Equal(2, r.UnmatchedLedger[0].VoucherItemId);
        Assert.Single(r.UnmatchedStatement);
        Assert.Equal(999_000, r.UnmatchedStatement[0].Amount);
    }

    [Fact]
    public void Same_Amount_Same_Date_Matches_One_To_One()
    {
        // دو تراکنش هم‌مبلغ و هم‌تاریخ → هر دو باید جدا منطبق شوند (نه دوبار با یکی)
        var ledger = new[]
        {
            new LedgerLine(1, "1404/03/01", 300_000),
            new LedgerLine(2, "1404/03/01", 300_000),
        };
        var statement = new[]
        {
            new StatementLine("1404/03/01", 300_000),
            new StatementLine("1404/03/01", 300_000),
        };

        var r = BankReconciliation.AutoMatch(ledger, statement);

        Assert.Equal(2, r.Matched.Count);
        Assert.Empty(r.UnmatchedLedger);
        Assert.Empty(r.UnmatchedStatement);
    }

    [Fact]
    public void Parser_Reads_Lines_Skips_Header_And_Empty()
    {
        var csv = "تاریخ,مبلغ,شرح\n" +
                  "1404/03/01,1000000,واریز\n" +
                  "\n" +
                  "1404/03/02,500000\n";

        var lines = BankStatementParser.Parse(csv);

        Assert.Equal(2, lines.Count);
        Assert.Equal("1404/03/01", lines[0].Date);
        Assert.Equal(1_000_000, lines[0].Amount);
        Assert.Equal("واریز", lines[0].Reference);
        Assert.Null(lines[1].Reference);
    }

    [Fact]
    public void Parser_Handles_Persian_Digits_And_Thousands_Separators()
    {
        var csv = "۱۴۰۴/۰۳/۰۱,۱،۰۰۰،۰۰۰";
        var lines = BankStatementParser.Parse(csv);

        Assert.Single(lines);
        Assert.Equal("1404/03/01", lines[0].Date);
        Assert.Equal(1_000_000, lines[0].Amount);
    }
}
