using SamaHesab.Application.Reports.Export;

namespace SamaHesab.Application.Common.Interfaces;

/// <summary>
/// تولیدِ PDFِ بومیِ فارسی (راست‌چین، با شکل‌دهیِ صحیحِ حروف). فاز ۱۱ — P2/DT-7.
/// پیاده‌سازی در لایهٔ Infrastructure با QuestPDF انجام می‌شود تا دسکتاپ/وب/API
/// همگی PDFِ یکسان تولید کنند.
/// </summary>
public interface IPdfService
{
    /// <summary>یک جدولِ گزارشِ عمومی را به PDF تبدیل می‌کند.</summary>
    /// <param name="table">عنوان + سرستون‌ها + ردیف‌ها.</param>
    /// <param name="meta">اطلاعاتِ سربرگ/پابرگِ اختیاری (نامِ شرکت، زیرعنوان، تاریخ).</param>
    byte[] RenderTable(ReportTable table, PdfMeta? meta = null);

    /// <summary>یک سندِ HTML (خروجیِ موتورِ قالبِ اسناد) را به PDF تبدیل می‌کند.</summary>
    byte[] RenderHtmlDocument(string html, PdfMeta? meta = null);
}

/// <summary>اطلاعاتِ تکمیلیِ سربرگ/پابرگِ PDF.</summary>
public sealed record PdfMeta(
    string? CompanyName = null,
    string? Subtitle = null,
    string? GeneratedAt = null,
    bool Landscape = false);
