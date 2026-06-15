using SamaHesab.Domain.Common;

namespace SamaHesab.Domain.Entities.Documents;

/// <summary>
/// فاز ۱۰ — قالبِ پویای سند (Document Template). یک سند، یک صفحهٔ هاردکد نیست؛ یک «قالب» است.
/// هر نوعِ سند (فاکتور فروش/خرید، رسید، چک، واچر…) می‌تواند چندین قالب داشته باشد و کاربر
/// پیش از چاپ یکی را انتخاب کند. محتوای قالب HTML با توکن است (مثلِ {InvoiceNumber}) و یک
/// بلوکِ تکرارِ ردیف برای اقلام. <see cref="DocumentType"/> کلیدِ رشته‌ای (پلاگین‌گونه) است تا
/// انواعِ نامحدود بدونِ تغییرِ کد پشتیبانی شود.
/// </summary>
public class DocumentTemplate : AuditableEntity
{
    /// <summary>کلیدِ نوعِ سند، مثل: SalesInvoice, PurchaseInvoice, SalesReturn, Quotation, PosReceipt …</summary>
    public string DocumentType { get; private set; } = default!;
    public string Name { get; private set; } = default!;
    /// <summary>A4P | A4L | A5 | Thermal80 | Thermal58 | Custom</summary>
    public string PaperSize { get; private set; } = "A4P";
    public string? HeaderHtml { get; private set; }
    public string BodyHtml { get; private set; } = default!;
    public string? FooterHtml { get; private set; }
    public bool IsDefault { get; private set; }
    public bool IsActive { get; private set; } = true;
    /// <summary>قالبِ سیستمی (پیش‌فرض/seed) — نباید حذف شود.</summary>
    public bool IsSystem { get; private set; }

    private DocumentTemplate() { }

    public static DocumentTemplate Create(int companyId, string documentType, string name,
        string bodyHtml, string paperSize = "A4P", string? headerHtml = null, string? footerHtml = null,
        bool isDefault = false, bool isSystem = false)
    {
        if (string.IsNullOrWhiteSpace(documentType)) throw new ArgumentException("نوعِ سند الزامی است.");
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("نامِ قالب الزامی است.");
        if (string.IsNullOrWhiteSpace(bodyHtml)) throw new ArgumentException("بدنهٔ قالب الزامی است.");
        return new DocumentTemplate
        {
            CompanyId = companyId,
            DocumentType = documentType.Trim(),
            Name = name.Trim(),
            BodyHtml = bodyHtml,
            PaperSize = string.IsNullOrWhiteSpace(paperSize) ? "A4P" : paperSize.Trim(),
            HeaderHtml = headerHtml,
            FooterHtml = footerHtml,
            IsDefault = isDefault,
            IsSystem = isSystem,
        };
    }

    public void Update(string name, string bodyHtml, string paperSize, string? headerHtml, string? footerHtml)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("نامِ قالب الزامی است.");
        if (string.IsNullOrWhiteSpace(bodyHtml)) throw new ArgumentException("بدنهٔ قالب الزامی است.");
        Name = name.Trim();
        BodyHtml = bodyHtml;
        PaperSize = string.IsNullOrWhiteSpace(paperSize) ? "A4P" : paperSize.Trim();
        HeaderHtml = headerHtml;
        FooterHtml = footerHtml;
        UpdatedAt = DateTime.Now;
    }

    public void SetDefault(bool isDefault) { IsDefault = isDefault; UpdatedAt = DateTime.Now; }
    public void SetActive(bool active) { IsActive = active; UpdatedAt = DateTime.Now; }
}
