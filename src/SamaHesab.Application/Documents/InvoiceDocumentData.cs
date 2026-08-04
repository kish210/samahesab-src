using System.Globalization;
using SamaHesab.Application.Sales.Queries;
using SamaHesab.Application.Purchase.Queries;

namespace SamaHesab.Application.Documents;

/// <summary>
/// U-WEB-TEMPLATES-BIND — تأمینِ دادهٔ فاکتورِ فروش/خرید برایِ موتورِ قالب (DocumentTemplateEngine).
/// معادلِ AccountingDocumentData ولی برایِ فاکتور — نقطهٔ اتصالِ «قالب‌هایِ چاپ» به چاپِ واقعیِ
/// فاکتور (پیش‌تر قالب‌ها فقط با دادهٔ نمونه پیش‌نمایش می‌شدند، به رندرِ واقعی وصل نبودند).
/// </summary>
public static class InvoiceDocumentData
{
    private static string M(decimal v) => v.ToString("#,0", CultureInfo.InvariantCulture);

    public static DocumentData FromSalesInvoice(SalesInvoiceDetailDto inv, string? companyName)
    {
        var fields = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["InvoiceNumber"] = inv.Number,
            ["InvoiceDate"] = inv.Date,
            ["CustomerName"] = inv.CustomerName ?? $"#{inv.CustomerId}",
            ["PriceLevel"] = inv.PriceLevel,
            ["Reference"] = inv.Reference ?? "",
            ["Description"] = inv.Description ?? "",
            ["Shipping"] = M(inv.Shipping),
            ["OtherCosts"] = M(inv.OtherCosts),
            ["Discount"] = M(inv.InvoiceDiscount),
            ["TotalAmount"] = M(inv.GrandTotal),
            ["PaidAmount"] = M(inv.PaidAmount),
            ["RemainAmount"] = M(inv.RemainAmount),
            ["BranchName"] = companyName ?? "",
        };
        var rows = inv.Items.Select(it => (IReadOnlyDictionary<string, string?>)new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["ProductCode"] = it.Code,
            ["ProductName"] = it.Name,
            ["Quantity"] = M(it.Quantity),
            ["UnitPrice"] = M(it.UnitPrice),
            ["LineTotal"] = M(it.Quantity * it.UnitPrice * (1 - it.DiscountPct / 100) * (1 + it.TaxPct / 100)),
        }).ToList();
        return DocumentData.Of(fields, rows);
    }

    public static DocumentData FromPurchaseInvoice(PurchaseInvoiceDetailDto inv, string? companyName)
    {
        var fields = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["InvoiceNumber"] = inv.Number,
            ["InvoiceDate"] = inv.Date,
            ["CustomerName"] = inv.SupplierName ?? $"#{inv.SupplierId}",
            ["Description"] = inv.Description ?? "",
            ["Shipping"] = M(inv.Shipping),
            ["OtherCosts"] = M(inv.OtherCosts),
            ["TotalAmount"] = M(inv.GrandTotal),
            ["PaidAmount"] = M(inv.PaidAmount),
            ["RemainAmount"] = M(inv.RemainAmount),
            ["BranchName"] = companyName ?? "",
        };
        var rows = inv.Items.Select(it => (IReadOnlyDictionary<string, string?>)new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["ProductCode"] = it.Code,
            ["ProductName"] = it.Name,
            ["Quantity"] = M(it.Quantity),
            ["UnitPrice"] = M(it.UnitPrice),
            ["LineTotal"] = M(it.Quantity * it.UnitPrice * (1 - it.DiscountPct / 100) * (1 + it.TaxPct / 100)),
        }).ToList();
        return DocumentData.Of(fields, rows);
    }
}
