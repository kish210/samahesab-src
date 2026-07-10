using SamaHesab.Domain.Common;

namespace SamaHesab.Modules.Contracting.Domain;

/// <summary>
/// آیتمِ اضافیِ یک‌بارهٔ همان‌دوره برایِ یک قراردادِ خدماتی — مثلاً کالایی که در همان ماه
/// جداگانه فروخته شده و کاربر می‌خواهد در فاکتورِ ماهانهٔ همان مشتری بیاید، نه فاکتورِ جدا.
/// تا وقتی IsConsumed=false است، در تولیدِ فاکتورِ بعدیِ قرارداد لحاظ می‌شود.
/// </summary>
public class ServiceContractExtraItem : AuditableEntity
{
    public int ServiceContractId { get; private set; }
    public int ProductId { get; private set; }
    public decimal Quantity { get; private set; }
    public decimal UnitPrice { get; private set; }
    public decimal TaxPct { get; private set; }
    public bool IsConsumed { get; private set; }

    private ServiceContractExtraItem() { }

    public static ServiceContractExtraItem Create(int companyId, int serviceContractId, int productId,
        decimal quantity, decimal unitPrice, decimal taxPct = 0)
    {
        if (serviceContractId <= 0) throw new ArgumentException("قرارداد الزامی است.");
        if (productId <= 0) throw new ArgumentException("کالا/خدمت الزامی است.");
        if (quantity <= 0) throw new ArgumentException("تعداد باید بزرگ‌تر از صفر باشد.");
        return new ServiceContractExtraItem
        {
            CompanyId = companyId, ServiceContractId = serviceContractId, ProductId = productId,
            Quantity = quantity, UnitPrice = unitPrice, TaxPct = taxPct
        };
    }

    public void MarkConsumed() { IsConsumed = true; UpdatedAt = DateTime.Now; }
}
