using SamaHesab.Domain.Entities.Accounting;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>منطق خالصِ سال مالی (هستهٔ ERP — قفل دوره).</summary>
public class FiscalYearTests
{
    private static FiscalYear Make() => FiscalYear.Create(1, "۱۴۰۴", "1404/01/01", "1404/12/29");

    [Fact]
    public void Contains_DateInsideRange_True()
    {
        var fy = Make();
        Assert.True(fy.Contains("1404/01/01"));   // مرز شروع
        Assert.True(fy.Contains("1404/06/15"));
        Assert.True(fy.Contains("1404/12/29"));   // مرز پایان
    }

    [Fact]
    public void Contains_DateOutsideRange_False()
    {
        var fy = Make();
        Assert.False(fy.Contains("1403/12/29"));  // قبل از شروع
        Assert.False(fy.Contains("1405/01/01"));  // بعد از پایان
    }

    [Fact]
    public void Create_EndBeforeStart_Throws()
        => Assert.Throws<System.ArgumentException>(
            () => FiscalYear.Create(1, "بد", "1404/12/29", "1404/01/01"));

    [Fact]
    public void Close_SetsClosedAndInactive_And_Reopen_Reverts()
    {
        var fy = Make();
        fy.Close();
        Assert.True(fy.IsClosed);
        Assert.False(fy.IsActive);

        fy.Reopen();
        Assert.False(fy.IsClosed);
    }

    [Fact]
    public void Update_OnClosedYear_Throws()
    {
        var fy = Make();
        fy.Close();
        Assert.Throws<System.InvalidOperationException>(
            () => fy.Update("۱۴۰۴", "1404/01/01", "1404/12/29"));
    }

    [Fact]
    public void Project_Create_Requires_Code_And_Name()
    {
        Assert.Throws<System.ArgumentException>(() => Project.Create(1, "", "نام"));
        Assert.Throws<System.ArgumentException>(() => Project.Create(1, "P1", ""));
        var p = Project.Create(1, "P1", "پروژهٔ آزمایشی", budget: 1_000_000);
        Assert.Equal(1_000_000, p.Budget);
        Assert.True(p.IsActive);
    }

    [Fact]
    public void CostCenter_Create_And_Deactivate()
    {
        var cc = CostCenter.Create(1, "100", "اداری");
        Assert.True(cc.IsActive);
        cc.Deactivate();
        Assert.False(cc.IsActive);
    }
}
