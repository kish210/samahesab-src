using System.Globalization;
using SamaHesab.Application.Reports.Export;

namespace SamaHesab.Modules.Tourism.Application;

// ── ورودی‌های گزارش (DTOهای ساده؛ C1 از جداولِ Tur پر می‌کند) ──

/// <summary>یک ردیفِ ماندهٔ ودیعهٔ تأمین‌کننده.</summary>
public record SupplierDepositRow(string SupplierName, decimal Charged, decimal Drawn);

/// <summary>یک ردیفِ گزارشِ روزانهٔ فروشِ تأمین‌کننده.</summary>
public record DailySaleRow(string ProductName, decimal Quantity, int Passengers,
    decimal NetSale, decimal Cost, decimal Profit);

/// <summary>سودِ یک محصول/فروش (تجمیعی).</summary>
public record ProductProfitRow(string ProductName, decimal Quantity, decimal NetSale, decimal Cost, decimal Profit);

/// <summary>پورسانتِ ماهانهٔ یک فروشنده.</summary>
public record SellerCommissionRow(string SellerName, int SaleCount, decimal NetSale, decimal Commission);

/// <summary>عملکردِ فروشنده (تجمیعِ دوره).</summary>
public record SellerPerformanceRow(string SellerName, int SaleCount, decimal NetSale, decimal Profit, decimal Commission);

/// <summary>
/// سازنده‌های گزارشِ ماژولِ گردشگری — منطقِ خالص و تست‌پذیر، بدونِ وابستگی به DB.
/// خروجی `ReportTable` است که با `ReportExporter` به CSV/HTMLِ راست‌چین تبدیل می‌شود.
/// C1 کوئری‌های `Tur` را به ورودیِ این متدها می‌دهد (هم‌الگوی PayrollExportService).
/// </summary>
public static class TourismReportBuilder
{
    /// <summary>ماندهٔ ودیعهٔ هر تأمین‌کننده = شارژ − برداشت، با ردیفِ جمع.</summary>
    public static ReportTable SupplierDepositBalance(IEnumerable<SupplierDepositRow> rows)
    {
        var list = rows?.ToList() ?? new();
        var body = list.Select(r => new[]
        {
            r.SupplierName, N(r.Charged), N(r.Drawn), N(r.Charged - r.Drawn)
        }).ToList();
        body.Add(new[] { "جمع", N(list.Sum(r => r.Charged)), N(list.Sum(r => r.Drawn)),
            N(list.Sum(r => r.Charged - r.Drawn)) });
        return new ReportTable("ماندهٔ ودیعهٔ تأمین‌کنندگان",
            new[] { "تأمین‌کننده", "شارژ", "برداشت", "مانده" }, body);
    }

    /// <summary>گزارشِ روزانهٔ فروشِ یک تأمین‌کننده (با تعدادِ مسافر و سود).</summary>
    public static ReportTable DailyReport(string supplierName, string date, IEnumerable<DailySaleRow> rows)
    {
        var list = rows?.ToList() ?? new();
        var body = list.Select(r => new[]
        {
            r.ProductName, N(r.Quantity), N(r.Passengers), N(r.NetSale), N(r.Cost), N(r.Profit)
        }).ToList();
        body.Add(new[] { "جمع", N(list.Sum(r => r.Quantity)), N(list.Sum(r => r.Passengers)),
            N(list.Sum(r => r.NetSale)), N(list.Sum(r => r.Cost)), N(list.Sum(r => r.Profit)) });
        return new ReportTable($"گزارشِ روزانه — {supplierName} ({date})",
            new[] { "محصول", "تعداد", "مسافر", "فروشِ خالص", "بهای تمام‌شده", "سود" }, body);
    }

    /// <summary>سودِ محصول/فروش (مرتب بر سودِ نزولی).</summary>
    public static ReportTable ProductProfit(IEnumerable<ProductProfitRow> rows)
    {
        var list = (rows?.ToList() ?? new()).OrderByDescending(r => r.Profit).ToList();
        var body = list.Select(r => new[]
        {
            r.ProductName, N(r.Quantity), N(r.NetSale), N(r.Cost), N(r.Profit), Pct(r.Profit, r.NetSale)
        }).ToList();
        body.Add(new[] { "جمع", N(list.Sum(r => r.Quantity)), N(list.Sum(r => r.NetSale)),
            N(list.Sum(r => r.Cost)), N(list.Sum(r => r.Profit)),
            Pct(list.Sum(r => r.Profit), list.Sum(r => r.NetSale)) });
        return new ReportTable("سودِ محصولات",
            new[] { "محصول", "تعداد", "فروشِ خالص", "بهای تمام‌شده", "سود", "حاشیهٔ سود" }, body);
    }

    /// <summary>پورسانتِ ماهانهٔ فروشندگان.</summary>
    public static ReportTable MonthlyCommission(string period, IEnumerable<SellerCommissionRow> rows)
    {
        var list = rows?.ToList() ?? new();
        var body = list.Select(r => new[]
        {
            r.SellerName, N(r.SaleCount), N(r.NetSale), N(r.Commission)
        }).ToList();
        body.Add(new[] { "جمع", N(list.Sum(r => r.SaleCount)), N(list.Sum(r => r.NetSale)),
            N(list.Sum(r => r.Commission)) });
        return new ReportTable($"پورسانتِ فروشندگان — {period}",
            new[] { "فروشنده", "تعدادِ فروش", "فروشِ خالص", "پورسانت" }, body);
    }

    /// <summary>عملکردِ فروشندگان (مرتب بر فروشِ نزولی).</summary>
    public static ReportTable SellerPerformance(IEnumerable<SellerPerformanceRow> rows)
    {
        var list = (rows?.ToList() ?? new()).OrderByDescending(r => r.NetSale).ToList();
        var body = list.Select(r => new[]
        {
            r.SellerName, N(r.SaleCount), N(r.NetSale), N(r.Profit), N(r.Commission)
        }).ToList();
        body.Add(new[] { "جمع", N(list.Sum(r => r.SaleCount)), N(list.Sum(r => r.NetSale)),
            N(list.Sum(r => r.Profit)), N(list.Sum(r => r.Commission)) });
        return new ReportTable("عملکردِ فروشندگان",
            new[] { "فروشنده", "تعدادِ فروش", "فروشِ خالص", "سود", "پورسانت" }, body);
    }

    private static string N(decimal v) =>
        Math.Round(v, 0, MidpointRounding.AwayFromZero).ToString("#,##0", CultureInfo.InvariantCulture);

    private static string Pct(decimal part, decimal whole) =>
        whole == 0 ? "—" : (part / whole * 100m).ToString("0.#", CultureInfo.InvariantCulture) + "٪";
}
