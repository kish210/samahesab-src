namespace SamaHesab.Application.Inventory;

/// <summary>
/// INV-1 گام۴ — ترکیبِ خالصِ دو ردیفِ سندِ «بهای تمام‌شده/موجودی کالا» برای ثبتِ دائمیِ COGS.
/// منطقِ خالص و تست‌پذیر (بدونِ I/O)؛ مصرف‌کننده (سندِ فروش/برگشت/حواله) ردیف‌ها را به سند می‌افزاید.
/// </summary>
public static class PerpetualCogs
{
    /// <summary>یک ردیفِ سند: حساب + بدهکار/بستانکار + شرح.</summary>
    public readonly record struct Line(int AccountId, decimal Debit, decimal Credit, string Description);

    /// <summary>
    /// فروش (<paramref name="reverse"/>=false): بد «بهای تمام‌شده» / بس «موجودی کالا».
    /// برگشت از فروش (<paramref name="reverse"/>=true): معکوس (کالا به انبار بازمی‌گردد).
    /// بهای ≤۰ → فهرستِ خالی. مبلغ به ۲ رقم گرد می‌شود؛ دو ردیف همیشه متوازن‌اند.
    /// </summary>
    public static IReadOnlyList<Line> Build(decimal totalCost, int cogsAccountId, int inventoryAccountId, bool reverse)
    {
        if (totalCost <= 0) return System.Array.Empty<Line>();
        var amount = System.Math.Round(totalCost, 2);
        return reverse
            ? new[]
            {
                new Line(inventoryAccountId, amount, 0, "بازگشتِ موجودی کالا (برگشت از فروش)"),
                new Line(cogsAccountId, 0, amount, "معکوسِ بهای تمام‌شده (برگشت از فروش)"),
            }
            : new[]
            {
                new Line(cogsAccountId, amount, 0, "بهای تمام‌شدهٔ کالای فروش‌رفته"),
                new Line(inventoryAccountId, 0, amount, "کاهشِ موجودی کالا (فروش)"),
            };
    }
}
