using System.Linq;
using SamaHesab.Application.Automation;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>P2 — سازندهٔ پیامکِ یادآورِ بدهی/چک.</summary>
public class OverdueReminderBuilderTests
{
    [Fact]
    public void Skips_Rows_Without_Mobile_Or_NonPositive_Amount()
    {
        var rows = new[]
        {
            new ReminderInput("علی", "09120000000", 1_000_000, ReminderKind.OverdueDebt),
            new ReminderInput("بی‌موبایل", null, 500_000, ReminderKind.OverdueDebt),
            new ReminderInput("صفر", "0912", 0, ReminderKind.OverdueDebt),
        };
        var res = OverdueReminderBuilder.Build(rows, "شرکتِ نمونه");
        var r = Assert.Single(res);
        Assert.Equal("09120000000", r.Mobile);
        Assert.Equal("علی", r.PartyName);
    }

    [Fact]
    public void Debt_Message_Has_Amount_And_Company()
    {
        const string company = "شرکتِ آزمون";
        var r = OverdueReminderBuilder.Build(
            new[] { new ReminderInput("رضا", "0912", 2_500_000, ReminderKind.OverdueDebt) }, company).Single();
        Assert.Contains("2,500,000", r.Message);     // مبلغ (ASCII)
        Assert.Contains(company, r.Message);          // نامِ شرکت (round-trip از همان رشتهٔ ورودی)
        Assert.Equal("رضا", r.PartyName);             // طرف‌حساب از پراپرتی (نه متنِ پیام — مقاوم به نوعِ کاف/یای فارسی)
        Assert.Equal(ReminderKind.OverdueDebt, r.Kind);
    }

    [Fact]
    public void Cheque_Message_Includes_DueDate()
    {
        var r = OverdueReminderBuilder.Build(
            new[] { new ReminderInput("سارا", "0912", 9_000_000, ReminderKind.ChequeDueSoon, DueDate: "1405/05/10") }, "ش").Single();
        Assert.Contains("1405/05/10", r.Message);     // تاریخِ سررسید (ASCII)
        Assert.Contains("9,000,000", r.Message);      // مبلغ (ASCII)
        Assert.Equal(ReminderKind.ChequeDueSoon, r.Kind);
    }

    [Fact]
    public void Empty_Party_Falls_Back_To_Generic_Greeting()
    {
        var r = OverdueReminderBuilder.Build(
            new[] { new ReminderInput("", "0912", 100, ReminderKind.OverdueDebt) }, "ش").Single();
        Assert.Contains("مشتریِ گرامی", r.Message);
    }

    [Fact]
    public void Empty_Input_Yields_Empty_List()
        => Assert.Empty(OverdueReminderBuilder.Build(System.Array.Empty<ReminderInput>(), "ش"));
}
