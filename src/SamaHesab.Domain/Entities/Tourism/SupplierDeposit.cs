using SamaHesab.Domain.Common;

namespace SamaHesab.Domain.Entities.Tourism;

/// <summary>
/// TUR-C1-1 — رکوردِ شارژِ ودیعه/اعتبارِ نزدِ تأمین‌کننده (پیش‌پرداخت).
/// هر شارژ سندِ «Dr ودیعه / Cr بانک» می‌زند؛ ماندهٔ تأمین‌کننده = جمعِ شارژها − برداشت‌های فروش.
/// </summary>
public class SupplierDeposit : AuditableEntity
{
    public int SupplierPartyId { get; private set; }
    public decimal Amount { get; private set; }
    public string Date { get; private set; } = default!;   // شمسی
    public string PaymentMethod { get; private set; } = "بانک";
    public int? VoucherId { get; private set; }            // سندِ شارژ (TreasuryRef)
    public string? Note { get; private set; }

    private SupplierDeposit() { }

    public static SupplierDeposit Create(int companyId, int supplierPartyId, decimal amount, string date,
        string paymentMethod = "بانک", string? note = null)
    {
        if (supplierPartyId <= 0) throw new ArgumentException("تأمین‌کننده الزامی است.");
        if (amount <= 0) throw new ArgumentException("مبلغِ شارژ باید بزرگ‌تر از صفر باشد.");
        if (string.IsNullOrWhiteSpace(date)) throw new ArgumentException("تاریخ الزامی است.");
        return new SupplierDeposit
        {
            CompanyId = companyId, SupplierPartyId = supplierPartyId, Amount = amount,
            Date = date, PaymentMethod = paymentMethod, Note = note
        };
    }

    public void SetVoucher(int voucherId) { VoucherId = voucherId; SetAudit(null); }
}
