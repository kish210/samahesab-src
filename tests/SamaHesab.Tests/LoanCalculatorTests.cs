using SamaHesab.Domain.Entities.Accounting;
using SamaHesab.Domain.Enums;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>U-LOAN — تستِ واحدِ جدولِ اقساطِ وام (بدونِ DB).</summary>
public class LoanCalculatorTests
{
    [Fact]
    public void Zero_Interest_Equal_Payment_Is_Principal_Divided_By_Term()
    {
        Assert.Equal(1000000m, LoanCalculator.EqualPayment(12000000m, 0m, 12), 2);
    }

    [Fact]
    public void Monthly_Rate_Is_Annual_Percent_Divided_By_1200()
    {
        // ۲۴٪ سالانه → ۲٪ ماهانه
        Assert.Equal(0.02m, LoanCalculator.MonthlyRate(24m), 4);
    }

    [Fact]
    public void Schedule_Sums_To_Principal_And_Ends_At_Zero()
    {
        var schedule = LoanCalculator.BuildSchedule(100000000m, 24m, 12);

        Assert.Equal(12, schedule.Count);
        Assert.Equal(100000000m, schedule.Sum(i => i.Principal), 2);
        Assert.Equal(0m, schedule[^1].Remaining, 2);

        // هر قسط = اصل + بهرهٔ همان ردیف.
        Assert.All(schedule, i => Assert.Equal(i.Payment, i.Principal + i.Interest, 2));
    }

    [Fact]
    public void Schedule_Is_Empty_For_Invalid_Inputs()
    {
        Assert.Empty(LoanCalculator.BuildSchedule(0m, 10m, 12));
        Assert.Empty(LoanCalculator.BuildSchedule(1000000m, 10m, 0));
    }

    [Fact]
    public void Equal_Payment_Is_Zero_For_Invalid_Inputs()
    {
        Assert.Equal(0m, LoanCalculator.EqualPayment(0m, 10m, 12));
        Assert.Equal(0m, LoanCalculator.EqualPayment(1000000m, 10m, 0));
    }
}

/// <summary>U-FIN-NOTES — تستِ واحدِ موجودیتِ یادداشتِ صورتِ مالی (بدونِ DB).</summary>
public class FinancialStatementNoteTests
{
    [Fact]
    public void Create_Trims_Title_And_Sets_Type_And_Order()
    {
        var note = FinancialStatementNote.Create(1, FinancialStatementType.BalanceSheet, "  نقدینگی  ", "متن", 2);

        Assert.Equal("نقدینگی", note.Title);
        Assert.Equal(FinancialStatementType.BalanceSheet, note.StatementType);
        Assert.Equal(2, note.Order);
    }

    [Fact]
    public void Create_Rejects_Blank_Title()
    {
        Assert.Throws<ArgumentException>(() =>
            FinancialStatementNote.Create(1, FinancialStatementType.CashFlow, "   ", null, 0));
    }

    [Fact]
    public void Update_Changes_Title_Body_And_Order()
    {
        var note = FinancialStatementNote.Create(1, FinancialStatementType.IncomeStatement, "یادداشت", "قدیم", 1);
        note.Update("یادداشت نو", "جدید", 3);

        Assert.Equal("یادداشت نو", note.Title);
        Assert.Equal("جدید", note.Body);
        Assert.Equal(3, note.Order);
    }
}
