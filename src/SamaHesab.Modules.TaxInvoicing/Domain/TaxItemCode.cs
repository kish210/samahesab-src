using SamaHesab.Domain.Common;

namespace SamaHesab.Modules.TaxInvoicing.Domain;

/// <summary>
/// نگاشتِ هر کالا/خدمتِ هستهٔ فروش (<c>Product.Id</c>، رفرنسِ نرم — بدونِ navigation) به شناسهٔ رسمیِ
/// «نظامِ یکپارچهٔ کدینگِ کالا و خدماتِ ایران» + کدِ واحدِ اندازه‌گیریِ موردِ نیازِ سامانهٔ مودیان.
/// بدونِ این نگاشت، آن ردیفِ فاکتور قابلِ‌ارسال به سازمان نیست (فیلدهایِ اجباریِ استانداردِ صورتحساب).
/// Core (Product) از وجودِ این جدول اطلاع ندارد — تغییرِ Product برایِ این قابلیت لازم نبود.
/// </summary>
public class TaxItemCode : AuditableEntity
{
    public int ProductId { get; private set; }
    /// <summary>شناسهٔ کالا/خدمتِ رسمی (IAN Item ID) — رشته، چون طولش بسته به نوعِ کالا/خدمت فرق دارد.</summary>
    public string ItemId { get; private set; } = default!;
    /// <summary>کدِ واحدِ اندازه‌گیریِ رسمی (مثلاً «عدد»، «کیلوگرم») طبقِ جدولِ واحدهایِ سازمان.</summary>
    public string MeasurementUnitCode { get; private set; } = default!;

    private TaxItemCode() { }

    public static TaxItemCode Create(int companyId, int productId, string itemId, string measurementUnitCode)
    {
        if (productId <= 0) throw new ArgumentException("کالا الزامی است.");
        if (string.IsNullOrWhiteSpace(itemId)) throw new ArgumentException("شناسهٔ کالایِ رسمی الزامی است.");
        if (string.IsNullOrWhiteSpace(measurementUnitCode)) throw new ArgumentException("کدِ واحدِ اندازه‌گیری الزامی است.");
        return new TaxItemCode
        {
            CompanyId = companyId, ProductId = productId,
            ItemId = itemId.Trim(), MeasurementUnitCode = measurementUnitCode.Trim()
        };
    }

    public void Update(string itemId, string measurementUnitCode)
    {
        if (string.IsNullOrWhiteSpace(itemId)) throw new ArgumentException("شناسهٔ کالایِ رسمی الزامی است.");
        if (string.IsNullOrWhiteSpace(measurementUnitCode)) throw new ArgumentException("کدِ واحدِ اندازه‌گیری الزامی است.");
        ItemId = itemId.Trim();
        MeasurementUnitCode = measurementUnitCode.Trim();
        SetAudit(null);
    }
}
