using SamaHesab.Application.Accounting;
using SamaHesab.Domain.Entities.Accounting;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>فاز ۱۲ (RC) — قفلِ دورهٔ مالی (`FiscalPeriodGuard`).</summary>
public class FiscalPeriodGuardTests
{
    private static FiscalYear Year() => FiscalYear.Create(1, "۱۴۰۵", "1405/01/01", "1405/12/29");

    [Fact]
    public void Null_year_is_allowed_legacy()
        => Assert.Null(FiscalPeriodGuard.Check(null, "1405/05/01"));

    [Fact]
    public void Open_and_in_range_passes()
        => Assert.Null(FiscalPeriodGuard.Check(Year(), "1405/05/01"));

    [Fact]
    public void Date_out_of_range_is_blocked()
        => Assert.NotNull(FiscalPeriodGuard.Check(Year(), "1406/01/01"));

    [Fact]
    public void Closed_period_is_blocked()
    {
        var y = Year();
        y.Close();
        var msg = FiscalPeriodGuard.Check(y, "1405/05/01");
        Assert.NotNull(msg);
        Assert.Contains("بسته", msg);
    }
}
