using SamaHesab.Domain.Common;

namespace SamaHesab.Domain.Entities.Tourism;

/// <summary>
/// TUR-C1-1 — سربرگِ فروشِ گردشگری. سندِ متوازنِ خودکار می‌زند؛ بهای هر خط از ودیعهٔ تأمین‌کننده برداشت می‌شود.
/// </summary>
public class TourismSale : AuditableEntity, IBranchScoped
{
    public int BranchId { get; private set; }
    public string Date { get; private set; } = default!;     // شمسی
    public int? CustomerPartyId { get; private set; }
    public int SalespersonPartyId { get; private set; }
    public string PaymentMethod { get; private set; } = "نقدی";
    public decimal TotalSale { get; private set; }
    public decimal TotalDiscount { get; private set; }
    public decimal TotalCost { get; private set; }
    public decimal TotalProfit { get; private set; }
    public int? VoucherId { get; private set; }
    public string? Note { get; private set; }

    private readonly List<TourismSaleLine> _lines = new();
    public IReadOnlyCollection<TourismSaleLine> Lines => _lines.AsReadOnly();

    private TourismSale() { }

    public static TourismSale Create(int companyId, int branchId, string date, int salespersonPartyId,
        int? customerPartyId = null, string paymentMethod = "نقدی", string? note = null)
    {
        if (string.IsNullOrWhiteSpace(date)) throw new ArgumentException("تاریخ الزامی است.");
        if (salespersonPartyId <= 0) throw new ArgumentException("فروشنده الزامی است.");
        return new TourismSale
        {
            CompanyId = companyId, BranchId = branchId, Date = date,
            SalespersonPartyId = salespersonPartyId, CustomerPartyId = customerPartyId,
            PaymentMethod = paymentMethod, Note = note
        };
    }

    public void AddLine(TourismSaleLine line)
    {
        _lines.Add(line);
        Recalculate();
    }

    private void Recalculate()
    {
        TotalSale = _lines.Sum(l => l.Quantity * l.UnitSalePrice);
        TotalDiscount = _lines.Sum(l => l.DiscountAmount);
        TotalCost = _lines.Sum(l => l.Quantity * l.UnitCost);
        TotalProfit = _lines.Sum(l => l.LineProfit);
    }

    public void SetVoucher(int voucherId) { VoucherId = voucherId; SetAudit(null); }
}
