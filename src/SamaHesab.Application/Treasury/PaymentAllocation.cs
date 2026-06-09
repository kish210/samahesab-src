namespace SamaHesab.Application.Treasury;

/// <summary>یک سطر تخصیص: چه مبلغی از دریافتی روی این فاکتور نشست.</summary>
public record PaymentAllocationLine(int InvoiceId, decimal Applied);

/// <summary>
/// تخصیص خودکار وجهِ دریافتی به فاکتورهای باز — منطق خالص و تست‌پذیر.
/// قاعده: FIFO (قدیمی‌ترین فاکتور اول). ترتیب ورودی همان ترتیب تخصیص است،
/// پس فراخواننده باید فاکتورها را بر اساس تاریخ (و سپس شماره) مرتب بدهد.
/// </summary>
public static class PaymentAllocation
{
    public static (IReadOnlyList<PaymentAllocationLine> Lines, decimal Unapplied) AllocateFifo(
        decimal amount, IEnumerable<(int InvoiceId, decimal Remaining)> openInvoices)
    {
        var lines = new List<PaymentAllocationLine>();
        var left = amount;

        foreach (var inv in openInvoices)
        {
            if (left <= 0.01m) break;
            var apply = System.Math.Min(left, inv.Remaining);
            if (apply <= 0) continue;            // فاکتور بدون مانده را رد کن
            lines.Add(new PaymentAllocationLine(inv.InvoiceId, apply));
            left -= apply;
        }

        return (lines, left);                     // Unapplied = مبلغِ تخصیص‌نیافته (علی‌الحساب)
    }
}
