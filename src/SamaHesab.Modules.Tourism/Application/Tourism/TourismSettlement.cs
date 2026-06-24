namespace SamaHesab.Modules.Tourism.Application;

/// <summary>نقش حسابِ یک ردیف سند گردشگری (مستقل از کد حساب؛ نگاشت کد در Command).</summary>
public enum TourismAccountRole
{
    Cash,            // صندوق (بخش پرداخت‌شده)
    Receivable,      // حساب‌های دریافتنی (بخش نسیه از مشتری)
    TourismRevenue,  // درآمد گردشگری
    TourismCost,     // بهای تمام‌شده‌ی خدمات گردشگری
    SupplierPayable, // حساب‌های پرداختنی (به تأمین‌کننده‌ی خدمات)
    CommissionExpense, // هزینه‌ی کمیسیون (به فروشنده/آژانس واسط)
    CommissionPayable  // کمیسیون پرداختنی
}

/// <summary>یک ردیف پیشنهادیِ سند (نقش + بدهکار/بستانکار + شرح).</summary>
public record TourismVoucherLine(TourismAccountRole Role, decimal Debit, decimal Credit, string Description);

/// <summary>نتیجه‌ی تسویه‌ی گردشگری.</summary>
public record TourismSettlementResult(
    IReadOnlyList<TourismVoucherLine> Lines,
    decimal Commission, decimal NetProfit, decimal TotalDebit, decimal TotalCredit)
{
    public bool IsBalanced => TotalDebit == TotalCredit;
}

/// <summary>
/// موتور خالص تسویه‌ی عملیات گردشگری — منطق حسابداری مستقل و تست‌پذیر (M11).
/// از داده‌ی مالیِ یک رزرو، ردیف‌های سند متوازن تولید می‌کند:
///   فروش:   بدهکار صندوق(پرداخت‌شده)+دریافتنی(نسیه) / بستانکار درآمد گردشگری
///   هزینه:  بدهکار بهای خدمات / بستانکار پرداختنی تأمین‌کننده
///   کمیسیون: بدهکار هزینه‌ی کمیسیون / بستانکار کمیسیون پرداختنی
/// </summary>
public static class TourismSettlement
{
    public static TourismSettlementResult Build(
        decimal saleAmount, decimal costAmount, decimal commissionPercent, decimal paidAmount)
    {
        if (saleAmount < 0 || costAmount < 0) throw new ArgumentException("مبلغ منفی مجاز نیست.");
        if (paidAmount < 0) paidAmount = 0;
        if (paidAmount > saleAmount) paidAmount = saleAmount;

        var commission = decimal.Round(saleAmount * commissionPercent / 100m, 0);
        var receivable = saleAmount - paidAmount;

        var lines = new List<TourismVoucherLine>();

        // ── فروش به مشتری ──
        if (paidAmount > 0)
            lines.Add(new TourismVoucherLine(TourismAccountRole.Cash, paidAmount, 0, "دریافت نقدی بابت رزرو"));
        if (receivable > 0)
            lines.Add(new TourismVoucherLine(TourismAccountRole.Receivable, receivable, 0, "مانده دریافتنی از مشتری"));
        if (saleAmount > 0)
            lines.Add(new TourismVoucherLine(TourismAccountRole.TourismRevenue, 0, saleAmount, "درآمد خدمات گردشگری"));

        // ── بهای تمام‌شده / بدهی به تأمین‌کننده ──
        if (costAmount > 0)
        {
            lines.Add(new TourismVoucherLine(TourismAccountRole.TourismCost, costAmount, 0, "بهای خدمات تأمین‌کننده"));
            lines.Add(new TourismVoucherLine(TourismAccountRole.SupplierPayable, 0, costAmount, "بدهی به تأمین‌کننده‌ی خدمات"));
        }

        // ── کمیسیون ──
        if (commission > 0)
        {
            lines.Add(new TourismVoucherLine(TourismAccountRole.CommissionExpense, commission, 0, "هزینه‌ی کمیسیون فروش"));
            lines.Add(new TourismVoucherLine(TourismAccountRole.CommissionPayable, 0, commission, "کمیسیون پرداختنی"));
        }

        var totalDebit = lines.Sum(l => l.Debit);
        var totalCredit = lines.Sum(l => l.Credit);
        var net = saleAmount - costAmount - commission;

        return new TourismSettlementResult(lines, commission, net, totalDebit, totalCredit);
    }
}
