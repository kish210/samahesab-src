namespace SamaHesab.Modules.Tourism.Application;

/// <summary>یک مسافرِ واچر.</summary>
public record VoucherPassenger(string FullName, string? NationalIdOrPassport, string? Phone);

/// <summary>یک ردیفِ خدمتِ واچر (نام محصول + تأمین‌کننده + تاریخِ سفر + تعداد + مسافران).</summary>
public record VoucherServiceLine(
    string ProductName, string SupplierName, string? TravelDate,
    decimal Quantity, decimal UnitSalePrice,
    IReadOnlyList<VoucherPassenger> Passengers);

/// <summary>ورودیِ سربرگِ واچر.</summary>
public record VoucherHeader(
    int SaleId, string VoucherNo, string IssueDate,
    string CustomerName, string SalespersonName, string PaymentMethod);

/// <summary>سندِ واچرِ سفر — برای چاپ/تحویل به مشتری.</summary>
public record TravelVoucher(
    VoucherHeader Header,
    IReadOnlyList<VoucherServiceLine> Lines,
    int PassengerCount, decimal TotalSale, decimal TotalDiscount, decimal NetPayable);

/// <summary>
/// سازندهٔ واچرِ سفر (کارتِ مسافر/بلیت) — منطقِ خالص و تست‌پذیر. از سربرگ + خطوطِ خدمت
/// یک سندِ قابلِ‌چاپ می‌سازد: خدمات با تاریخِ سفر، فهرستِ مسافران، و جمعِ مبالغ.
/// مبالغ از خودِ ردیف‌ها بازمحاسبه می‌شود تا با snapshotِ فروش سازگار بماند.
/// </summary>
public static class TravelVoucherBuilder
{
    public static TravelVoucher Build(VoucherHeader header, IEnumerable<VoucherServiceLine> lines)
    {
        var list = lines.ToList();
        decimal totalSale = list.Sum(l => l.Quantity * l.UnitSalePrice);
        // تخفیف در این نما در سطحِ ردیف نگه‌داری نمی‌شود؛ خالص = جمعِ فروش (تخفیف در سربرگِ فروش لحاظ شده).
        int paxCount = list.Sum(l => l.Passengers.Count);

        return new TravelVoucher(header, list, paxCount, totalSale, 0, totalSale);
    }

    /// <summary>نسخهٔ کامل با تخفیفِ سربرگ (وقتی صداکننده تخفیفِ کلِ فروش را می‌داند).</summary>
    public static TravelVoucher Build(VoucherHeader header, IEnumerable<VoucherServiceLine> lines, decimal totalDiscount)
    {
        var v = Build(header, lines);
        var net = v.TotalSale - (totalDiscount < 0 ? 0 : totalDiscount);
        return v with { TotalDiscount = totalDiscount < 0 ? 0 : totalDiscount, NetPayable = net < 0 ? 0 : net };
    }
}
