using SamaHesab.Application.Accounting;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>طبقه‌بندی سررسید چک — تابلوی پیگیری وصول/پرداخت چک.</summary>
public class ChequeBoardTests
{
    [Theory]
    [InlineData("1404/03/01", "1404/03/10", ChequeDueState.Overdue)]   // سررسید گذشته
    [InlineData("1404/03/10", "1404/03/10", ChequeDueState.DueToday)]  // امروز
    [InlineData("1404/03/20", "1404/03/10", ChequeDueState.Upcoming)]  // پیش رو
    public void Classify_By_DueDate(string due, string today, ChequeDueState expected)
        => Assert.Equal(expected, ChequeBoard.Classify(due, today));

    [Fact]
    public void Year_Boundary_Is_Respected()
    {
        Assert.Equal(ChequeDueState.Overdue, ChequeBoard.Classify("1403/12/29", "1404/01/01"));
        Assert.Equal(ChequeDueState.Upcoming, ChequeBoard.Classify("1404/01/02", "1404/01/01"));
    }
}
