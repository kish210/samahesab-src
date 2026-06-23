using SamaHesab.Application.Tourism;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>نمای لیستِ فروشِ گردشگری — نزدیک‌ترین تاریخِ سفر + فیلترِ بازه.</summary>
public class TourismSalesViewTests
{
    [Fact]
    public void NearestTravelDate_Picks_Earliest_NonEmpty()
    {
        var d = TourismSalesView.NearestTravelDate(new[] { "1405/05/10", null, "1405/04/01", "  ", "1405/06/20" });
        Assert.Equal("1405/04/01", d);
    }

    [Fact]
    public void NearestTravelDate_All_Empty_Is_Null()
        => Assert.Null(TourismSalesView.NearestTravelDate(new string?[] { null, "", "   " }));

    [Theory]
    [InlineData("1405/03/15", null, null, true)]
    [InlineData("1405/03/15", "1405/03/01", "1405/03/31", true)]
    [InlineData("1405/02/28", "1405/03/01", null, false)]   // قبل از from
    [InlineData("1405/04/02", null, "1405/03/31", false)]   // بعد از to
    [InlineData("1405/03/01", "1405/03/01", "1405/03/01", true)]  // مرزها شامل
    public void InRange_Respects_Bounds(string date, string? from, string? to, bool expected)
        => Assert.Equal(expected, TourismSalesView.InRange(date, from, to));
}
