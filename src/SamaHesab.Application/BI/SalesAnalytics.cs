namespace SamaHesab.Application.BI;

/// <summary>یک رکورد فروش در سطح فاکتور (هدر).</summary>
public record SalesRecord(string Date, int PartyId, decimal Amount);

/// <summary>یک ردیف فروش در سطح کالا.</summary>
public record ProductSale(int ProductId, decimal Quantity, decimal NetAmount, decimal Profit);

/// <summary>یک ردیف رتبه‌بندی (مشتری/کالا/تأمین‌کننده).</summary>
public record RankRow(int Id, decimal Total, int Count, decimal Extra);

/// <summary>یک نقطه از روند زمانی.</summary>
public record TrendPoint(string Period, decimal Total, int Count);

/// <summary>
/// موتور هوش تجاری فروش — منطق خالص و تست‌پذیر (بدون دسترسی به داده).
/// تاریخ‌ها شمسیِ yyyy/MM/dd؛ کلید ماه = yyyy/MM با مقایسه‌ی لغوی.
/// </summary>
public static class SalesAnalytics
{
    /// <summary>پرفروش‌ترین طرف‌حساب‌ها بر اساس مجموع مبلغ (نزولی).</summary>
    public static List<RankRow> TopParties(IEnumerable<SalesRecord> records, int take = 10)
        => records
            .GroupBy(r => r.PartyId)
            .Select(g => new RankRow(g.Key, g.Sum(x => x.Amount), g.Count(), 0))
            .OrderByDescending(r => r.Total)
            .ThenBy(r => r.Id)
            .Take(take)
            .ToList();

    /// <summary>پرفروش‌ترین کالاها بر اساس مجموع مبلغ خالص (نزولی)؛ Extra = سود.</summary>
    public static List<RankRow> TopProducts(IEnumerable<ProductSale> items, int take = 10)
        => items
            .GroupBy(i => i.ProductId)
            .Select(g => new RankRow(g.Key, g.Sum(x => x.NetAmount), g.Count(),
                g.Sum(x => x.Profit)))
            .OrderByDescending(r => r.Total)
            .ThenBy(r => r.Id)
            .Take(take)
            .ToList();

    /// <summary>روند فروش ماهانه (yyyy/MM)، مرتب صعودی بر دوره.</summary>
    public static List<TrendPoint> MonthlyTrend(IEnumerable<SalesRecord> records)
        => records
            .GroupBy(r => MonthKey(r.Date))
            .Where(g => g.Key.Length > 0)
            .Select(g => new TrendPoint(g.Key, g.Sum(x => x.Amount), g.Count()))
            .OrderBy(p => p.Period)
            .ToList();

    /// <summary>مجموع سود ناخالص از ردیف‌های کالا.</summary>
    public static decimal TotalProfit(IEnumerable<ProductSale> items) => items.Sum(i => i.Profit);

    /// <summary>مجموع فروش.</summary>
    public static decimal TotalSales(IEnumerable<SalesRecord> records) => records.Sum(r => r.Amount);

    /// <summary>کلید ماه از تاریخ شمسی yyyy/MM/dd → yyyy/MM. ورودی نامعتبر → رشته‌ی خالی.</summary>
    public static string MonthKey(string persianDate)
        => (!string.IsNullOrEmpty(persianDate) && persianDate.Length >= 7 && persianDate[4] == '/')
            ? persianDate.Substring(0, 7)
            : string.Empty;
}
