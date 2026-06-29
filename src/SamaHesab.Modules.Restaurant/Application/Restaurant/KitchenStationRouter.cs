namespace SamaHesab.Modules.Restaurant.Application;

/// <summary>یک ردیفِ سفارش برای مسیریابی (کالا/نام/تعداد/یادداشت).</summary>
public record StationLine(int ProductId, string Name, decimal Qty, string? Notes);

/// <summary>توصیفِ یک ایستگاهِ چاپ (شناسه/نام/پرینتر/پیش‌فرض).</summary>
public record StationDef(int Id, string Name, string PrinterName, bool IsDefault);

/// <summary>تیکتِ یک ایستگاه = ردیف‌هایی که باید به پرینترِ آن ایستگاه چاپ شوند.</summary>
public record StationTicket(int StationId, string StationName, string PrinterName, IReadOnlyList<StationLine> Lines);

/// <summary>
/// مسیریابیِ ردیف‌های سفارش به ایستگاه‌های چاپ — منطقِ خالص و تست‌پذیر (بدونِ I/O).
/// هر کالا به ایستگاهِ نگاشته‌شده می‌رود؛ کالای بدونِ نگاشت به ایستگاهِ پیش‌فرض؛ اگر ایستگاهِ
/// پیش‌فرضی نباشد، به یک ایستگاهِ مجازیِ «بدون ایستگاه» (پرینترِ خالی = پیش‌فرضِ سیستم) تا چیزی گم نشود.
/// خروجی به ترتیبِ نامِ ایستگاه مرتب است؛ ردیف‌ها ترتیبِ ورودی را حفظ می‌کنند.
/// </summary>
public static class KitchenStationRouter
{
    public static IReadOnlyList<StationTicket> Route(
        IEnumerable<StationLine> lines,
        IReadOnlyDictionary<int, int> productToStation,
        IReadOnlyList<StationDef> stations)
    {
        var byId = stations.ToDictionary(s => s.Id);
        var def = stations.FirstOrDefault(s => s.IsDefault);
        var synthetic = new StationDef(0, "بدون ایستگاه", "", false);

        // ترتیبِ ظهورِ ایستگاه‌ها حفظ می‌شود؛ ردیف‌ها per ایستگاه جمع می‌شوند.
        var order = new List<int>();
        var groups = new Dictionary<int, List<StationLine>>();

        foreach (var l in lines)
        {
            StationDef st;
            if (productToStation.TryGetValue(l.ProductId, out var sid) && byId.TryGetValue(sid, out var mapped))
                st = mapped;
            else if (def is not null)
                st = def;
            else
                st = synthetic;

            if (!groups.TryGetValue(st.Id, out var list))
            {
                list = new List<StationLine>();
                groups[st.Id] = list;
                order.Add(st.Id);
            }
            list.Add(l);
        }

        StationDef Resolve(int id) => id == 0 ? synthetic : byId.GetValueOrDefault(id, synthetic);

        return order
            .Select(id => { var s = Resolve(id); return new StationTicket(s.Id, s.Name, s.PrinterName, groups[id]); })
            .OrderBy(t => t.StationName, System.StringComparer.Create(new System.Globalization.CultureInfo("fa-IR"), false))
            .ToList();
    }
}
