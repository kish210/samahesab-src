using SamaHesab.Application.Reports.Export;
using SamaHesab.Modules.Tourism.Application;
using Xunit;

namespace SamaHesab.Tests;

/// <summary>TUR-C2-3 — سازنده‌های گزارشِ گردشگری (خالص).</summary>
public class TourismReportBuilderTests
{
    [Fact]
    public void SupplierDeposit_Computes_Balance_And_Totals()
    {
        var t = TourismReportBuilder.SupplierDepositBalance(new[]
        {
            new SupplierDepositRow("آژانسِ الف", 10_000_000, 6_000_000),
            new SupplierDepositRow("آژانسِ ب", 5_000_000, 5_000_000),
        });
        Assert.Equal(3, t.Rows.Count);                 // ۲ ردیف + جمع
        Assert.Equal("4,000,000", t.Rows[0][3]);       // ماندهٔ ردیفِ اول
        var total = t.Rows[^1];
        Assert.Equal("جمع", total[0]);
        Assert.Equal("4,000,000", total[3]);           // جمعِ مانده = ۴م + ۰
    }

    [Fact]
    public void DailyReport_Sums_Passengers_And_Profit()
    {
        var t = TourismReportBuilder.DailyReport("آژانسِ الف", "1405/03/20", new[]
        {
            new DailySaleRow("گشتِ جزیره", 2, Passengers: 8, NetSale: 4_000_000, Cost: 3_000_000, Profit: 1_000_000),
            new DailySaleRow("بلیطِ پرواز", 3, Passengers: 3, NetSale: 9_000_000, Cost: 8_400_000, Profit: 600_000),
        });
        var total = t.Rows[^1];
        Assert.Equal("11", total[2]);                  // جمعِ مسافر ۸+۳
        Assert.Equal("1,600,000", total[5]);           // جمعِ سود
        Assert.Contains("آژانسِ الف", t.Title);
    }

    [Fact]
    public void ProductProfit_Sorted_Desc_And_Margin()
    {
        var t = TourismReportBuilder.ProductProfit(new[]
        {
            new ProductProfitRow("کم‌سود", 1, 1_000_000, 900_000, 100_000),
            new ProductProfitRow("پرسود", 1, 2_000_000, 1_000_000, 1_000_000),
        });
        Assert.Equal("پرسود", t.Rows[0][0]);           // مرتب بر سودِ نزولی
        Assert.Equal("50٪", t.Rows[0][5]);             // حاشیهٔ سودِ پرسود = ۱م/۲م
    }

    [Fact]
    public void MonthlyCommission_Totals()
    {
        var t = TourismReportBuilder.MonthlyCommission("خرداد ۱۴۰۵", new[]
        {
            new SellerCommissionRow("رضایی", 12, 50_000_000, 2_500_000),
            new SellerCommissionRow("محمدی", 8, 30_000_000, 1_500_000),
        });
        Assert.Equal("4,000,000", t.Rows[^1][3]);      // جمعِ پورسانت
        Assert.Contains("خرداد", t.Title);
    }

    [Fact]
    public void SellerPerformance_Sorted_By_Sale_Desc()
    {
        var t = TourismReportBuilder.SellerPerformance(new[]
        {
            new SellerPerformanceRow("کم‌فروش", 2, 10_000_000, 1_000_000, 500_000),
            new SellerPerformanceRow("پرفروش", 9, 90_000_000, 9_000_000, 4_500_000),
        });
        Assert.Equal("پرفروش", t.Rows[0][0]);
    }

    [Fact]
    public void Output_Renders_To_Csv()
    {
        var t = TourismReportBuilder.SupplierDepositBalance(new[]
        {
            new SupplierDepositRow("آژانسِ الف", 10_000_000, 6_000_000),
        });
        var csv = ReportExporter.ToCsv(t);
        Assert.Contains("تأمین‌کننده", csv);
        Assert.Contains("آژانسِ الف", csv);
    }
}
