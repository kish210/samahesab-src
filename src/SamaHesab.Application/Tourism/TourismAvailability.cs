namespace SamaHesab.Application.Tourism;

/// <summary>ورودیِ یک محصول برای محاسبهٔ ظرفیت (شناسه/نام/قیمتِ فروش/ظرفیت).</summary>
public record TourismProductInput(int ProductId, string Name, decimal SalePrice, int? Capacity, bool Active);

/// <summary>نمای ظرفیت/موجودیِ یک محصول برای فروشنده.</summary>
public record TourismAvailabilityRow(
    int ProductId, string Name, decimal SalePrice,
    int? Capacity, decimal Sold, decimal? Remaining, bool IsSoldOut)
{
    /// <summary>ظرفیت نامحدود است؟ (Capacity = null)</summary>
    public bool Unlimited => Capacity is null;
}

/// <summary>
/// محاسبهٔ ظرفیت/موجودیِ محصولاتِ گردشگری برای نمای فروشنده — منطقِ خالص و تست‌پذیر.
/// ماندهٔ ظرفیت = ظرفیتِ کل − جمعِ فروش‌رفته (تعداد در خطوطِ فروش). ظرفیتِ null = نامحدود (هیچ‌گاه تمام نمی‌شود).
/// فروشنده قیمت + ماندهٔ ظرفیت را می‌بیند؛ محصولِ تمام‌شده با IsSoldOut علامت می‌خورد.
/// </summary>
public static class TourismAvailability
{
    public static IReadOnlyList<TourismAvailabilityRow> Build(
        IEnumerable<TourismProductInput> products,
        IReadOnlyDictionary<int, decimal> soldByProduct,
        bool onlyActive = true)
    {
        var rows = new List<TourismAvailabilityRow>();
        foreach (var p in products)
        {
            if (onlyActive && !p.Active) continue;
            var sold = soldByProduct.TryGetValue(p.ProductId, out var s) ? s : 0m;
            decimal? remaining = p.Capacity.HasValue ? Math.Max(0, p.Capacity.Value - sold) : (decimal?)null;
            var soldOut = p.Capacity.HasValue && sold >= p.Capacity.Value;
            rows.Add(new TourismAvailabilityRow(p.ProductId, p.Name, p.SalePrice, p.Capacity, sold, remaining, soldOut));
        }
        return rows.OrderBy(r => r.Name, StringComparer.Create(new System.Globalization.CultureInfo("fa-IR"), false)).ToList();
    }
}
