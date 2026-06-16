namespace SamaHesab.Application.Documents;

/// <summary>
/// فاز ۱۱ — P3/DT-8: سخت‌سازیِ مارکت‌پلیسِ قالب.
/// اعتبارسنجی/نسخه‌بندیِ بسته‌های `.shtpl` پیش از واردکردن: فرمت/نسخهٔ مجاز + فیلدهای لازم.
/// منطقِ خالص و تست‌پذیر (بدونِ UI/EF) تا هر دو مسیرِ واردات (تک‌فایل و نصبِ گروهیِ گالری)
/// از یک قاعده پیروی کنند.
/// <para>
/// عمداً نوعِ سند را به فهرستِ ثابتی محدود نمی‌کنیم: کاتالوگِ نوع‌های سند توسطِ C2 (طراح/گالری)
/// به‌مرور گسترش می‌یابد و قفل‌کردنِ آن این‌جا، قالب‌های معتبرِ تازه را بی‌صدا رد می‌کرد. تنها
/// لازم است نوعِ سند خالی نباشد؛ نوعِ ناشناخته بی‌ضرر است (صرفاً دکمهٔ چاپِ متناظر نخواهد داشت).
/// </para>
/// </summary>
public static class DocumentTemplatePackageValidator
{
    public readonly record struct Result(bool Ok, string? Error)
    {
        public static Result Pass() => new(true, null);
        public static Result Fail(string error) => new(false, error);
    }

    /// <summary>یک بستهٔ قالب را اعتبارسنجی می‌کند. در صورتِ مشکل، پیامِ فارسیِ گویا برمی‌گرداند.</summary>
    public static Result Validate(DocumentTemplatePackage? pkg)
    {
        if (pkg is null)
            return Result.Fail("فایلِ قالب خوانده نشد یا ساختارِ معتبری ندارد.");

        if (string.IsNullOrWhiteSpace(pkg.Format))
            return Result.Fail("فرمتِ بسته مشخص نیست (فیلدِ Format خالی است).");

        if (!string.Equals(pkg.Format, DocumentTemplatePackage.CurrentFormat, StringComparison.OrdinalIgnoreCase))
            return Result.Fail($"نسخهٔ فرمتِ قالب پشتیبانی نمی‌شود: «{pkg.Format}» (موردِ انتظار: {DocumentTemplatePackage.CurrentFormat}).");

        if (string.IsNullOrWhiteSpace(pkg.DocumentType))
            return Result.Fail("نوعِ سندِ قالب مشخص نشده است.");

        if (string.IsNullOrWhiteSpace(pkg.Name))
            return Result.Fail("نامِ قالب الزامی است.");

        if (string.IsNullOrWhiteSpace(pkg.BodyHtml))
            return Result.Fail("بدنهٔ قالب (BodyHtml) خالی است.");

        return Result.Pass();
    }
}
